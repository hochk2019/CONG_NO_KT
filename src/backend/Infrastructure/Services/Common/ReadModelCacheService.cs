using System.Text;
using System.Text.Json;
using CongNoGolden.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Infrastructure.Services.Common;

public sealed class ReadModelCacheService : IReadModelCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly ReadModelCacheOptions _options;
    private readonly ILogger<ReadModelCacheService> _logger;

    public ReadModelCacheService(
        IDistributedCache cache,
        IOptions<ReadModelCacheOptions> options,
        ILogger<ReadModelCacheService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string namespaceKey,
        string cacheKey,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct)
    {
        if (!_options.Enabled || ttl <= TimeSpan.Zero)
        {
            return await factory(ct);
        }

        var version = await GetNamespaceVersionAsync(namespaceKey, ct);
        var fullKey = BuildDataKey(namespaceKey, version, cacheKey);

        var cached = await _cache.GetAsync(fullKey, ct);
        if (cached is not null)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<T>(cached, JsonOptions);
                if (payload is not null)
                {
                    return payload;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Read-model cache payload invalid for key {CacheKey}.", fullKey);
            }
        }

        var value = await factory(ct);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);

        await _cache.SetAsync(
            fullKey,
            bytes,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            },
            ct);

        return value;
    }

    public async Task InvalidateNamespaceAsync(string namespaceKey, CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var versionKey = BuildNamespaceVersionKey(namespaceKey);
        var newVersion = NewVersion();
        await _cache.SetAsync(
            versionKey,
            Encoding.UTF8.GetBytes(newVersion),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(Math.Max(1, _options.NamespaceVersionHours))
            },
            ct);
    }

    private async Task<string> GetNamespaceVersionAsync(string namespaceKey, CancellationToken ct)
    {
        var versionKey = BuildNamespaceVersionKey(namespaceKey);
        var bytes = await _cache.GetAsync(versionKey, ct);
        if (bytes is not null && bytes.Length > 0)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        var newVersion = NewVersion();
        await _cache.SetAsync(
            versionKey,
            Encoding.UTF8.GetBytes(newVersion),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(Math.Max(1, _options.NamespaceVersionHours))
            },
            ct);

        return newVersion;
    }

    private static string BuildNamespaceVersionKey(string namespaceKey)
    {
        var safeNamespace = Normalize(namespaceKey);
        return $"congno:cache:namespace:{safeNamespace}:version";
    }

    private static string BuildDataKey(string namespaceKey, string version, string key)
    {
        var safeNamespace = Normalize(namespaceKey);
        var safeKey = Normalize(key);
        return $"congno:cache:data:{safeNamespace}:{version}:{safeKey}";
    }

    private static string NewVersion()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "default";
        }

        var normalized = value.Trim().ToLowerInvariant();
        normalized = normalized.Replace(" ", "_", StringComparison.Ordinal);
        normalized = normalized.Replace("|", "_", StringComparison.Ordinal);
        return normalized;
    }
}
