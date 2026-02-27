using CongNoGolden.Api.Middleware;
using CongNoGolden.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Tests.Unit;

public sealed class ReadModelCacheInvalidationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_InvalidatesNamespaces_ForSuccessfulMutation()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/receipts/123/approve";

        var cache = new StubReadModelCache();
        var middleware = new ReadModelCacheInvalidationMiddleware(
            async _ =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                await Task.CompletedTask;
            },
            NullLogger<ReadModelCacheInvalidationMiddleware>.Instance);

        await middleware.InvokeAsync(context, cache);

        Assert.Equal(new[] { "dashboard", "reports", "risk" }, cache.InvalidatedNamespaces);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotInvalidate_ForGetRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/dashboard/overview";

        var cache = new StubReadModelCache();
        var middleware = new ReadModelCacheInvalidationMiddleware(
            async _ =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                await Task.CompletedTask;
            },
            NullLogger<ReadModelCacheInvalidationMiddleware>.Instance);

        await middleware.InvokeAsync(context, cache);

        Assert.Empty(cache.InvalidatedNamespaces);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotInvalidate_ForFailedMutation()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/imports/abc/commit";

        var cache = new StubReadModelCache();
        var middleware = new ReadModelCacheInvalidationMiddleware(
            async _ =>
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await Task.CompletedTask;
            },
            NullLogger<ReadModelCacheInvalidationMiddleware>.Instance);

        await middleware.InvokeAsync(context, cache);

        Assert.Empty(cache.InvalidatedNamespaces);
    }

    private sealed class StubReadModelCache : IReadModelCache
    {
        public List<string> InvalidatedNamespaces { get; } = new();

        public Task<T> GetOrCreateAsync<T>(
            string namespaceKey,
            string cacheKey,
            TimeSpan ttl,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken ct)
        {
            return factory(ct);
        }

        public Task InvalidateNamespaceAsync(string namespaceKey, CancellationToken ct)
        {
            InvalidatedNamespaces.Add(namespaceKey);
            return Task.CompletedTask;
        }
    }
}
