using System.Diagnostics;
using Microsoft.Extensions.Primitives;

namespace CongNoGolden.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 64;

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers[HeaderName]);

        context.TraceIdentifier = correlationId;
        context.Items[HeaderName] = correlationId;
        context.Request.Headers[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            var activity = Activity.Current;
            activity?.SetTag("correlation.id", correlationId);
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(StringValues values)
    {
        if (values.Count == 0)
        {
            return CreateCorrelationId();
        }

        var value = values[0]?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return CreateCorrelationId();
        }

        if (value.Length > MaxLength)
        {
            return CreateCorrelationId();
        }

        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch) && ch is not '-' and not '_' and not '.')
            {
                return CreateCorrelationId();
            }
        }

        return value;
    }

    private static string CreateCorrelationId()
    {
        return Guid.NewGuid().ToString("N");
    }
}
