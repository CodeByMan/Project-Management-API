using Serilog.Context;
using System.Text.RegularExpressions;

namespace ProjectManager.Middleware
{
    public class CorrelationIdMiddleware
    {
        public const string CorrelationIdHeader = "X-Correlation-ID";
        public const string CorrelationIdItemKey = "CorrelationId";
        private const int MaximumLength = 64;
        private static readonly Regex ValidCorrelationId = new(
            $"^[A-Za-z0-9._-]{{1,{MaximumLength}}}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var incomingValue = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();
            var correlationId = NormalizeOrCreate(incomingValue);

            context.Items[CorrelationIdItemKey] = correlationId;
            context.TraceIdentifier = correlationId;
            context.Response.Headers[CorrelationIdHeader] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }

        public static string NormalizeOrCreate(string? value)
        {
            var trimmed = value?.Trim();
            return !string.IsNullOrEmpty(trimmed) && ValidCorrelationId.IsMatch(trimmed)
                ? trimmed
                : Guid.NewGuid().ToString("N");
        }
    }
}
