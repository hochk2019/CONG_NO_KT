using CongNoGolden.Application.Common.Interfaces;

namespace CongNoGolden.Api.Middleware;

public sealed class MaintenanceMiddleware
{
    private readonly RequestDelegate _next;

    public MaintenanceMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IMaintenanceState maintenanceState)
    {
        if (!maintenanceState.IsActive)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path;
        if (path.StartsWithSegments("/health") ||
            path.StartsWithSegments("/admin/backup") ||
            path.StartsWithSegments("/auth"))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new
        {
            title = "Maintenance",
            status = StatusCodes.Status503ServiceUnavailable,
            detail = maintenanceState.Message ?? "He thong dang phuc hoi du lieu.",
            code = "MAINTENANCE_MODE"
        });
    }
}
