using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Shared.Infrastructure.Middleware;

public class CorrelationIdMiddleware
{
    private const string Header = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // use existing ID if forwarded from Gateway
        // otherwise create a new one
        var correlationId = context.Request.Headers[Header].FirstOrDefault()
                            ?? Guid.NewGuid().ToString();

        // attach to response so client can trace it
        context.Response.Headers[Header] = correlationId;

        // attach to HttpContext so controllers/middleware can access it
        context.Items["CorrelationId"] = correlationId;

        // push into Serilog — every log inside here gets CorrelationId automatically
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}