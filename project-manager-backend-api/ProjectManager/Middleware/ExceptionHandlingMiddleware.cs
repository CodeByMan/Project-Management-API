using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace ProjectManager.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
            catch (Exception ex)
            {
                if (context.Response.HasStarted)
                {
                    _logger.LogError(ex, "An unhandled exception occurred after the response started.");
                    throw;
                }

                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdItemKey]?.ToString()
                ?? context.TraceIdentifier;

            _logger.LogError(ex, "An unhandled exception occurred. CorrelationId: {CorrelationId}", correlationId);

            context.Response.Clear();
            context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = correlationId;
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var problem = new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = "An unexpected error occurred.",
                Detail = "The request could not be completed. Provide the correlation ID to support.",
                Instance = context.Request.Path
            };
            problem.Extensions["correlationId"] = correlationId;

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
    }
}
