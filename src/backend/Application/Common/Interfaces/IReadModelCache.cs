namespace CongNoGolden.Application.Common.Interfaces;

public interface IReadModelCache
{
    Task<T> GetOrCreateAsync<T>(
        string namespaceKey,
        string cacheKey,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct);

    Task InvalidateNamespaceAsync(string namespaceKey, CancellationToken ct);
}
