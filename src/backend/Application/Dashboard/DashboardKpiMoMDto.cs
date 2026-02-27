namespace CongNoGolden.Application.Dashboard;

public sealed record DashboardKpiMoMDto(
    DashboardKpiDeltaDto TotalOutstanding,
    DashboardKpiDeltaDto OutstandingInvoice,
    DashboardKpiDeltaDto OutstandingAdvance,
    DashboardKpiDeltaDto OverdueTotal,
    DashboardKpiDeltaDto UnallocatedReceiptsAmount,
    DashboardKpiDeltaDto OnTimeCustomers);
