using Contracts.Events;
using FocusTrack.Gateway;
using FocusTrack.Gateway.Consumer;
using FocusTrack.Gateway.Middleware;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Shared.Infrastructure.Middleware;
using System.Security.Claims;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Show PII only in Development to see the actual token errors
if (builder.Environment.IsDevelopment())
{
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
}

// ----------------------------------SERILOG
// structured JSON logging — every log line is a JSON object 
// CorrelationId is automatically included via the CorrelationIdMiddleware 
builder.Host.UseSerilog((ctx, config) =>
{
    config
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
});

builder.Services.AddControllers();  // needed for AuthController (login/logout)
builder.Services.AddHealthChecks(); // needed for /healthz and /readyz endpoints

//MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<UserStatusChangedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]!);
            h.Password(builder.Configuration["RabbitMQ:Password"]!);
        });

        cfg.ConfigureEndpoints(ctx);
    });
});

// AUTHENTICATION
// Cookie = stores the session after login
// OpenIdConnect = handles the OIDC flow with Auth0
builder.Services.AddAuthentication(options =>
{
    // after login, use a cookie to keep the user signed in
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    // when login is needed, redirect to Auth0
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "MyGateway.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;

    // for API requests return 401 instead of redirecting to login page
    options.Events.OnRedirectToLogin = ctx =>
    {
        var isAPIRequest = ctx.Request.Headers["Accept"].ToString().Contains("application/json");
        if (isAPIRequest)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
})
.AddOpenIdConnect(options =>
{
    options.NonceCookie.SameSite = SameSiteMode.None;
    options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    // Auth0 tenant URL
    options.Authority = builder.Configuration["Auth:Authority"];
    options.ClientId = builder.Configuration["Auth:ClientId"];
    options.ClientSecret = builder.Configuration["Auth:ClientSecret"];

    if (builder.Environment.IsDevelopment())
        options.RequireHttpsMetadata = false;

    options.CallbackPath = "/signin-oidc"; // after login, Auth0 will redirect back to this endpoint
    options.ResponseType = OpenIdConnectResponseType.Code; // authorization code flow — most secure flow for web apps
    options.UsePkce = true; // PKCE — prevents authorization code interception attacks
    options.SaveTokens = true; // store access/refresh tokens in the cookie
    options.GetClaimsFromUserInfoEndpoint = true; // fetch additional user info from Auth0 after login

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Scope.Add("offline_access");

    options.TokenValidationParameters = new TokenValidationParameters
    {
        NameClaimType = "name",
        // tell ASP.NET Core where to find roles in the token
        // must match the namespace used in the Auth0 Action
        RoleClaimType = "https://focustrack-api/roles"
    };

    options.Events = new OpenIdConnectEvents
    {
        OnRedirectToIdentityProvider = ctx =>
        {
            // CRITICAL: This ensures Auth0 sends a JWT, not an Opaque token
            ctx.ProtocolMessage.SetParameter("audience", builder.Configuration["Auth:Audience"]);

            // Handles prompt=login for forced login screen
            if (ctx.Properties.Items.TryGetValue("prompt", out var prompt))
                ctx.ProtocolMessage.Prompt = prompt;
            return Task.CompletedTask;

        },

        // handles logout — redirects to Auth0 to clear its session too
        // without this only the local cookie is cleared
        // and the user can still access protected resources
        OnRedirectToIdentityProviderForSignOut = ctx =>
        {
            var logoutUri =
                $"https://{builder.Configuration["Auth:Domain"]}/v2/logout" +
                $"?client_id={builder.Configuration["Auth:ClientId"]}" +
                $"&returnTo={Uri.EscapeDataString("https://localhost:5000")}";

            ctx.Response.Redirect(logoutUri);
            ctx.HandleResponse();
            return Task.CompletedTask;
        },

        // logs auth failures with correlation ID
        // never exposes stack traces to the client
        OnAuthenticationFailed = ctx =>
        {
            Log.Warning(
                "OIDC authentication failed. Reason: {Reason}",
                ctx.Exception.Message);
            ctx.HandleResponse();
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        },

        OnTokenValidated = async ctx =>
        {
            Log.Information("Token validated for user: {User}", ctx.Principal.Identity.Name);
            var userId = ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = ctx.Principal?.FindFirst(ClaimTypes.Email)?.Value;

            if (userId != null)
            {
                // Check suspension via Session service HTTP call
                var httpClient = ctx.HttpContext.RequestServices
                    .GetRequiredService<IHttpClientFactory>()
                    .CreateClient("SessionService");

                var encodedId = Uri.EscapeDataString(userId);
                var isSuspended = await httpClient
                    .GetFromJsonAsync<bool>($"internal/users/{encodedId}/is-suspended");

                if (isSuspended)
                {
                    ctx.Fail("User is suspended.");
                    return;
                }

                var bus = ctx.HttpContext.RequestServices
                    .GetRequiredService<IPublishEndpoint>();
                await bus.Publish(new UserLoggedInEvent(userId, email ?? ""));
            }
        }
    };
});

// -----------------------------------------------------------------------------------------------------YARP REVERSE PROXY
// routes incoming requests to the correct service also forwards the Bearer token and CorrelationId to downstream services
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(ctx =>
    {
        ctx.AddRequestTransform(async transform =>
        {
            // forward correlation ID
            var correlationId = transform.HttpContext.Items["CorrelationId"]?.ToString();
            if (!string.IsNullOrEmpty(correlationId))
                transform.ProxyRequest.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

            // forward bearer token
            var token = await transform.HttpContext.GetTokenAsync("access_token");
            Console.WriteLine($"[DEBUG] Forwarding Token: {token.Substring(0, Math.Min(20, token.Length))}...");
            if (!string.IsNullOrEmpty(token))
                transform.ProxyRequest.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        });
    });
builder.Services.AddSingleton<SuspendedUsers>();

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.None;
    options.Secure = CookieSecurePolicy.Always;
});
var app = builder.Build();

// PDF Requirement: Global Exception Handler (No stack traces to client)
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        var correlationId = context.Items["CorrelationId"];
        Log.Error(ex, "Unhandled Gateway Exception. CorrelationId: {CorrelationId}", correlationId);
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { error = "Internal Server Error", correlationId });
    }
});

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseCookiePolicy();      
app.UseAuthentication();
app.UseMiddleware<TokenRefreshMiddleware>();
app.UseAuthorization();
app.UseMiddleware<SuspensionCheckMiddleware>();
app.MapControllers();
app.MapReverseProxy().RequireAuthorization();
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");
app.Run();
