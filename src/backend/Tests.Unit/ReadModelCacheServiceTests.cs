using CongNoGolden.Infrastructure.Services.Common;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Tests.Unit;

public sealed class ReadModelCacheServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_UsesCachedValue_OnSecondRequest()
    {
        var cache = CreateCache();
        var service = CreateService(cache);
        var calls = 0;

        var first = await service.GetOrCreateAsync(
            "reports",
            "test-key",
            TimeSpan.FromMinutes(5),
            _ =>
            {
                calls++;
                return Task.FromResult(new SamplePayload("first"));
            },
            CancellationToken.None);

        var second = await service.GetOrCreateAsync(
            "reports",
            "test-key",
            TimeSpan.FromMinutes(5),
            _ =>
            {
                calls++;
                return Task.FromResult(new SamplePayload("second"));
            },
            CancellationToken.None);

        Assert.Equal("first", first.Value);
        Assert.Equal("first", second.Value);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task InvalidateNamespaceAsync_ForcesFactoryToRunAgain()
    {
        var cache = CreateCache();
        var service = CreateService(cache);
        var calls = 0;

        await service.GetOrCreateAsync(
            "dashboard",
            "overview",
            TimeSpan.FromMinutes(5),
            _ =>
            {
                calls++;
                return Task.FromResult(new SamplePayload("value"));
            },
            CancellationToken.None);

        await service.InvalidateNamespaceAsync("dashboard", CancellationToken.None);

        await service.GetOrCreateAsync(
            "dashboard",
            "overview",
            TimeSpan.FromMinutes(5),
            _ =>
            {
                calls++;
                return Task.FromResult(new SamplePayload("value"));
            },
            CancellationToken.None);

        Assert.Equal(2, calls);
    }

    private static IDistributedCache CreateCache()
    {
        return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    }

    private static ReadModelCacheService CreateService(IDistributedCache cache)
    {
        return new ReadModelCacheService(
            cache,
            Options.Create(new ReadModelCacheOptions
            {
                Enabled = true,
                NamespaceVersionHours = 24
            }),
            NullLogger<ReadModelCacheService>.Instance);
    }

    private sealed record SamplePayload(string Value);
}
