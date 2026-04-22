using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using System.Globalization;

namespace FocusTrack.Gateway.Middleware;

public class TokenRefreshMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenRefreshMiddleware> _logger;

    public TokenRefreshMiddleware(
        RequestDelegate next,
        ILogger<TokenRefreshMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var expiresAt = await context.GetTokenAsync("expires_at");

        if (!string.IsNullOrEmpty(expiresAt))
        {
            var expiry = DateTimeOffset.Parse(
                expiresAt, CultureInfo.InvariantCulture);

            // check if token expires in less than 1 minute
            if (expiry < DateTimeOffset.UtcNow.AddMinutes(1))
            {
                var refreshToken = await context.GetTokenAsync("refresh_token");

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    _logger.LogInformation(
                        "Access token expiring, attempting silent refresh");

                    try
                    {
                        // force re-authentication which triggers
                        // Auth0 to issue a new access token using refresh token
                        await context.ChallengeAsync(
                            OpenIdConnectDefaults.AuthenticationScheme,
                            new AuthenticationProperties
                            {
                                RedirectUri = context.Request.Path,
                                IsPersistent = true
                            });
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            "Silent refresh failed: {Reason}", ex.Message);

                        // clear cookie and force re-login
                        await context.SignOutAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme);

                        context.Response.StatusCode =
                            StatusCodes.Status401Unauthorized;
                        return;
                    }
                }
                else
                {
                    _logger.LogWarning("No refresh token available");
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
        }

        await _next(context);
    }
}