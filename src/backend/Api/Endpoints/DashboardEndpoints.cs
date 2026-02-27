using CongNoGolden.Application.Dashboard;
using CongNoGolden.Application.Common.Interfaces;

namespace CongNoGolden.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard/overview", async (
            DateOnly? from,
            DateOnly? to,
            int? months,
            int? top,
            string? trendGranularity,
            int? trendPeriods,
            IDashboardService service,
            IReadModelCache cache,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var request = new DashboardOverviewRequest(from, to, months, top, trendGranularity, trendPeriods);
            var cacheKey = EndpointCacheKeys.ForHttpRequest(httpContext);
            var result = await cache.GetOrCreateAsync(
                "dashboard",
                cacheKey,
                TimeSpan.FromSeconds(30),
                token => service.GetOverviewAsync(request, token),
                ct);
            return Results.Ok(result);
        })
        .WithName("DashboardOverview")
        .WithTags("Dashboard")
        .RequireAuthorization("ReportsView");

        app.MapGet("/dashboard/overdue-groups", async (
            DateOnly? asOf,
            int? top,
            string? groupBy,
            IDashboardService service,
            IReadModelCache cache,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var request = new DashboardOverdueGroupRequest(asOf, top, groupBy);
            var cacheKey = EndpointCacheKeys.ForHttpRequest(httpContext);
            var result = await cache.GetOrCreateAsync(
                "dashboard",
                cacheKey,
                TimeSpan.FromSeconds(30),
                token => service.GetOverdueGroupsAsync(request, token),
                ct);
            return Results.Ok(result);
        })
        .WithName("DashboardOverdueGroups")
        .WithTags("Dashboard")
        .RequireAuthorization("ReportsView");

        app.MapGet("/dashboard/preferences", async (
            IDashboardService service,
            ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await service.GetPreferencesAsync(currentUser.UserId.Value, ct);
            return Results.Ok(result);
        })
        .WithName("DashboardPreferences")
        .WithTags("Dashboard")
        .RequireAuthorization("ReportsView");

        app.MapPut("/dashboard/preferences", async (
            UpdateDashboardPreferencesRequest request,
            IDashboardService service,
            ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await service.UpdatePreferencesAsync(currentUser.UserId.Value, request, ct);
            return Results.Ok(result);
        })
        .WithName("DashboardPreferencesUpdate")
        .WithTags("Dashboard")
        .RequireAuthorization("ReportsView");

        return app;
    }
}
