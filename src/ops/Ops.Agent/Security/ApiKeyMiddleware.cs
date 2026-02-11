using Microsoft.AspNetCore.Http;

namespace Ops.Agent.Security;

public sealed class ApiKeyMiddleware(RequestDelegate next, string apiKey)
{
    private const string HeaderName = "X-Api-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var value) || value != apiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        await next(context);
    }
}
