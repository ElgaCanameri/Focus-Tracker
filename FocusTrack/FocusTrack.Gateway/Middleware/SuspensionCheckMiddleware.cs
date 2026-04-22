using System.Security.Claims;

namespace FocusTrack.Gateway.Middleware
{
    public class SuspensionCheckMiddleware
    {
        private readonly RequestDelegate _next;

        public SuspensionCheckMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, SuspendedUsers cache)
        {
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId != null && cache.IsSuspended(userId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Account suspended.");
                return;
            }

            await _next(context);
        }
    }
}
