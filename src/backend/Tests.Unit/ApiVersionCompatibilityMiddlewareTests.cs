using CongNoGolden.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Tests.Unit;

public sealed class ApiVersionCompatibilityMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_RewritesVersionedPath_ToLegacyRoute()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/receipts";

        PathString? pathSeenByNext = null;
        var middleware = new ApiVersionCompatibilityMiddleware(nextContext =>
        {
            pathSeenByNext = nextContext.Request.Path;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal("/receipts", pathSeenByNext?.Value);
        Assert.Equal("v1", context.Response.Headers[ApiVersionCompatibilityMiddleware.VersionHeaderName].ToString());
        Assert.False(context.Response.Headers.ContainsKey(ApiVersionCompatibilityMiddleware.DeprecationHeaderName));
    }

    [Fact]
    public async Task InvokeAsync_AddsDeprecationHeaders_ForUnversionedApiRoute()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/receipts";

        var middleware = new ApiVersionCompatibilityMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal("unversioned", context.Response.Headers[ApiVersionCompatibilityMiddleware.VersionHeaderName].ToString());
        Assert.Equal("true", context.Response.Headers[ApiVersionCompatibilityMiddleware.DeprecationHeaderName].ToString());
        Assert.False(string.IsNullOrWhiteSpace(context.Response.Headers[ApiVersionCompatibilityMiddleware.SunsetHeaderName].ToString()));
        Assert.Contains("/api/v1/receipts", context.Response.Headers[ApiVersionCompatibilityMiddleware.SuccessorLinkHeaderName].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotAddDeprecation_ForHealthRoute()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/health/ready";

        var middleware = new ApiVersionCompatibilityMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal("unversioned", context.Response.Headers[ApiVersionCompatibilityMiddleware.VersionHeaderName].ToString());
        Assert.False(context.Response.Headers.ContainsKey(ApiVersionCompatibilityMiddleware.DeprecationHeaderName));
    }
}
