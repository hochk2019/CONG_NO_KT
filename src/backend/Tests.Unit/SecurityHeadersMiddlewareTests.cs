using CongNoGolden.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Tests.Unit;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AddsBaselineSecurityHeaders()
    {
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var headers = context.Response.Headers;
        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", headers["X-Frame-Options"]);
        Assert.Equal("no-referrer", headers["Referrer-Policy"]);
        Assert.Equal("camera=(), microphone=(), geolocation=()", headers["Permissions-Policy"]);
        Assert.Equal("none", headers["X-Permitted-Cross-Domain-Policies"]);
        Assert.Equal("same-origin", headers["Cross-Origin-Opener-Policy"]);
        Assert.Equal("same-site", headers["Cross-Origin-Resource-Policy"]);
        Assert.Contains("default-src 'none'", headers["Content-Security-Policy"].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_AddsHstsOnlyForHttpsRequests()
    {
        var httpsContext = new DefaultHttpContext();
        httpsContext.Request.Scheme = "https";
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpsContext);
        Assert.Equal(
            "max-age=31536000; includeSubDomains",
            httpsContext.Response.Headers["Strict-Transport-Security"]);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";

        await middleware.InvokeAsync(httpContext);
        Assert.False(httpContext.Response.Headers.ContainsKey("Strict-Transport-Security"));
    }
}
