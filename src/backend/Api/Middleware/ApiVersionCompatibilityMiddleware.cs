namespace CongNoGolden.Api.Middleware;

public sealed class ApiVersionCompatibilityMiddleware
{
    public const string VersionHeaderName = "X-Api-Version";
    public const string DeprecationHeaderName = "Deprecation";
    public const string SunsetHeaderName = "Sunset";
    public const string SuccessorLinkHeaderName = "Link";

    private const string CurrentVersion = "v1";
    private const string VersionPrefix = "/api/v1";
    private const string SunsetValue = "Tue, 30 Jun 2026 23:59:59 GMT";

    private static readonly string[] ExcludedPrefixes =
    [
        "/health",
        "/swagger",
        "/metrics"
    ];

    private readonly RequestDelegate _next;

    public ApiVersionCompatibilityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var originalPath = context.Request.Path;
        var isVersionedRequest = false;
        var shouldAddDeprecation = false;

        if (originalPath.StartsWithSegments(VersionPrefix, out var remainingPath))
        {
            isVersionedRequest = true;
            context.Request.Path = string.IsNullOrWhiteSpace(remainingPath.Value) ? "/" : remainingPath;
        }
        else if (ShouldMarkUnversionedDeprecated(originalPath))
        {
            shouldAddDeprecation = true;
        }

        context.Response.Headers[VersionHeaderName] = isVersionedRequest ? CurrentVersion : "unversioned";
        if (shouldAddDeprecation)
        {
            ApplyDeprecationHeaders(context.Response.Headers, originalPath);
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[VersionHeaderName] = isVersionedRequest ? CurrentVersion : "unversioned";
            if (shouldAddDeprecation)
            {
                ApplyDeprecationHeaders(context.Response.Headers, originalPath);
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static bool ShouldMarkUnversionedDeprecated(PathString path)
    {
        if (!path.HasValue || path == "/")
        {
            return false;
        }

        var raw = path.Value!;
        foreach (var prefix in ExcludedPrefixes)
        {
            if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static void ApplyDeprecationHeaders(IHeaderDictionary headers, PathString originalPath)
    {
        headers[DeprecationHeaderName] = "true";
        headers[SunsetHeaderName] = SunsetValue;

        var successorPath = $"{VersionPrefix}{originalPath.Value}";
        headers[SuccessorLinkHeaderName] = $"<{successorPath}>; rel=\"successor-version\"";
    }
}
