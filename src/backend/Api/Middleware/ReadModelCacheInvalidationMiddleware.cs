using CongNoGolden.Application.Common.Interfaces;

namespace CongNoGolden.Api.Middleware;

public sealed class ReadModelCacheInvalidationMiddleware
{
    private static readonly string[] Namespaces = ["dashboard", "reports", "risk"];

    private static readonly string[] MutatingPathPrefixes =
    [
        "/imports",
        "/advances",
        "/receipts",
        "/invoices",
        "/period-locks",
        "/risk/rules",
        "/reminders",
        "/admin/health/reconcile-balances",
        "/admin/health/run-retention"
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<ReadModelCacheInvalidationMiddleware> _logger;

    public ReadModelCacheInvalidationMiddleware(
        RequestDelegate next,
        ILogger<ReadModelCacheInvalidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IReadModelCache cache)
    {
        await _next(context);

        if (!ShouldInvalidate(context))
        {
            return;
        }

        foreach (var namespaceKey in Namespaces)
        {
            try
            {
                await cache.InvalidateNamespaceAsync(namespaceKey, context.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Unable to invalidate read-model cache namespace {NamespaceKey}.",
                    namespaceKey);
            }
        }
    }

    private static bool ShouldInvalidate(HttpContext context)
    {
        var method = context.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            return false;
        }

        var statusCode = context.Response.StatusCode;
        if (statusCode < StatusCodes.Status200OK || statusCode >= StatusCodes.Status300MultipleChoices)
        {
            return false;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        return MutatingPathPrefixes.Any(prefix =>
            path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
