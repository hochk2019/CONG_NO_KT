using Microsoft.AspNetCore.Http;
using Ops.Agent.Security;
using System.Runtime.Versioning;

namespace Ops.Tests;

[SupportedOSPlatform("windows")]
public class ApiKeyMiddlewareTests
{
    [Fact]
    public async Task Rejects_WhenMissingApiKey()
    {
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = new ApiKeyMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, "secret");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Allows_WhenApiKeyMatches()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "secret";
        var nextCalled = false;
        var middleware = new ApiKeyMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, "secret");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }
}
