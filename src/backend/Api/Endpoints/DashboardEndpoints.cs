using CongNoGolden.Application.Dashboard;

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
            CancellationToken ct) =>
        {
            var request = new DashboardOverviewRequest(from, to, months, top, trendGranularity, trendPeriods);
            var result = await service.GetOverviewAsync(request, ct);
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
            CancellationToken ct) =>
        {
            var request = new DashboardOverdueGroupRequest(asOf, top, groupBy);
            var result = await service.GetOverdueGroupsAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("DashboardOverdueGroups")
        .WithTags("Dashboard")
        .RequireAuthorization("ReportsView");

        return app;
    }
}
