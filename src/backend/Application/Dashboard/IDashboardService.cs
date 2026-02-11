namespace CongNoGolden.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardOverviewDto> GetOverviewAsync(DashboardOverviewRequest request, CancellationToken ct);
    Task<IReadOnlyList<DashboardOverdueGroupItem>> GetOverdueGroupsAsync(
        DashboardOverdueGroupRequest request,
        CancellationToken ct);
}
