using Microsoft.AspNetCore.Http;
using ProjectManager.Middleware;
using ProjectManager.Services;

namespace ProjectManager.Tests;

public class AuditAndCorrelationTests
{
    [Fact]
    public void AuditSerialization_RedactsPasswordsTokensAndSecrets()
    {
        var serialized = AuditValueSanitizer.Serialize(new
        {
            Email = "user@example.com",
            Password = "PlainTextPassword",
            ResetToken = "reset-token-value",
            Jwt = "jwt-value",
            Nested = new { ApiKey = "api-key-value" }
        });

        Assert.NotNull(serialized);
        Assert.Contains("user@example.com", serialized);
        Assert.DoesNotContain("PlainTextPassword", serialized);
        Assert.DoesNotContain("reset-token-value", serialized);
        Assert.DoesNotContain("jwt-value", serialized);
        Assert.DoesNotContain("api-key-value", serialized);
        Assert.Contains("[REDACTED]", serialized);
    }

    [Fact]
    public async Task CorrelationMiddleware_PreservesValidIncomingId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = "request-123_ABC";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal("request-123_ABC", context.TraceIdentifier);
        Assert.Equal("request-123_ABC", context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeader]);
    }

    [Fact]
    public async Task CorrelationMiddleware_ReplacesInvalidOrOversizedId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = new string('x', 65) + "\r\nInjected";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Matches("^[a-f0-9]{32}$", context.TraceIdentifier);
        Assert.DoesNotContain("Injected", context.TraceIdentifier);
    }
}
