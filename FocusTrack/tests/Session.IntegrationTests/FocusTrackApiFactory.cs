using System.Security.Claims;
using System.Text.Encodings.Web;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Session.Infrastructure;

namespace Session.IntegrationTests;

/// <summary>
/// Test WebApplicationFactory that:
///   1. Replaces the SQL Server DbContext with an EF InMemory one (shared per factory instance).
///   2. Replaces MassTransit with its in-memory test harness (no RabbitMQ required).
///   3. Replaces the Auth0 JWT bearer with a fake auth scheme that reads the user id from the
///      <c>X-Test-User</c> header and the role(s) from <c>X-Test-Role</c>.
/// </summary>
public sealed class FocusTrackApiFactory : WebApplicationFactory<Program>
{
    public const string TestUserHeader = "X-Test-User";
    public const string TestRoleHeader = "X-Test-Role";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "InMemory",
                ["RabbitMQ:Host"] = "unused",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
                ["Auth:Authority"] = "https://tests.local/",
                ["Auth:Audience"] = "https://focustrack-api",
            });
        });

        builder.ConfigureServices(services =>
        {
            // --- Swap DbContext to InMemory --------------------------------
            RemoveAll<DbContextOptions<AppDbContext>>(services);
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseInMemoryDatabase("ft-integration-shared")
                   .ConfigureWarnings(w => w.Ignore(
                       Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));

            // --- Swap MassTransit for the in-memory test harness -----------
            RemoveAllMassTransit(services);
            services.AddMassTransitTestHarness();

            // --- Replace JwtBearer with a fake header-based auth scheme ----
            services.RemoveAll<IConfigureOptions<AuthenticationOptions>>();
            services
                .AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            // Map the real JwtBearer scheme name to the test scheme as well, so [Authorize] with
            // no explicit scheme and [Authorize(Policy = "AdminOnly")] both resolve correctly.
            services.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme = "Test";
                o.DefaultScheme = "Test";
                o.SchemeMap.Remove(JwtBearerDefaults.AuthenticationScheme);
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Ensure the InMemory DB is created once per factory instance; the default Program.cs
        // calls db.Database.Migrate() which is a no-op on InMemory but can throw if the model
        // has been resolved to Relational. Using EnsureCreated is the safe path.
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        return host;
    }

    private static void RemoveAll<T>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    private static void RemoveAllMassTransit(IServiceCollection services)
    {
        // Remove anything MassTransit-shaped so we can cleanly add the test harness.
        var toRemove = services
            .Where(d => d.ServiceType.FullName is { } n
                && (n.StartsWith("MassTransit.", StringComparison.Ordinal)))
            .ToList();
        foreach (var d in toRemove) services.Remove(d);

        // IPublishEndpoint + IBus are registered by MassTransit and by the harness.
        RemoveAll<IPublishEndpoint>(services);
        RemoveAll<IBus>(services);
        RemoveAll<ISendEndpointProvider>(services);
    }
}

/// <summary>
/// Auth scheme that trusts the <c>X-Test-User</c> / <c>X-Test-Role</c> headers. Only
/// registered by the integration test factory. Production code never sees this.
/// </summary>
internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(FocusTrackApiFactory.TestUserHeader, out var userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        if (Request.Headers.TryGetValue(FocusTrackApiFactory.TestRoleHeader, out var roles))
        {
            foreach (var r in roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries))
                claims.Add(new Claim(ClaimTypes.Role, r));
        }

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, "Test")));
    }
}
