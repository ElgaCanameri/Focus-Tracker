using Session.Application.Common;
using System.Net;
using System.Text.Json;

namespace Session.Presentation.Middleware
{
    public class ExceptionHandler
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandler> _logger;

        public ExceptionHandler(
            RequestDelegate next,
            ILogger<ExceptionHandler> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (NotFoundException ex)
            {
                await WriteErrorResponse(
                    context, HttpStatusCode.NotFound, ex.Message);
            }
            catch (ForbiddenException ex)
            {
                await WriteErrorResponse(
                    context, HttpStatusCode.Forbidden, ex.Message);
            }
            catch (Exception ex)
            {
                var correlationId = context.Items["CorrelationId"]?.ToString();

                // log full exception internally with correlationId
                _logger.LogError(
                    ex,
                    "Unhandled exception. CorrelationId: {CorrelationId}",
                    correlationId);

                // never expose stack trace to client
                await WriteErrorResponse(
                    context,
                    HttpStatusCode.InternalServerError,
                    "An unexpected error occurred.",
                    correlationId);
            }
        }

        private static async Task WriteErrorResponse(
            HttpContext context,
            HttpStatusCode statusCode,
            string message,
            string? correlationId = null)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var response = new
            {
                status = (int)statusCode,
                message,
                correlationId
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response));
        }
    }
}
