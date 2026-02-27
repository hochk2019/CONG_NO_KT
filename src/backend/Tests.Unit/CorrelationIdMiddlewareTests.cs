using CongNoGolden.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Tests.Unit;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationId_WhenMissing()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.True(context.Response.Headers.TryGetValue(CorrelationIdMiddleware.HeaderName, out var responseHeader));
        var value = responseHeader.ToString();
        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.Equal(value, context.TraceIdentifier);
        Assert.Equal(value, context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_UsesIncomingCorrelationId_WhenValid()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "abc-123.DEF";

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal("abc-123.DEF", context.TraceIdentifier);
        Assert.Equal("abc-123.DEF", context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_ReplacesIncomingCorrelationId_WhenInvalid()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "invalid id with spaces";

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        var value = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.NotEqual("invalid id with spaces", value);
        Assert.Equal(value, context.TraceIdentifier);
        Assert.Equal(value, context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }
}
