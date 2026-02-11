namespace CongNoGolden.Application.Dashboard;

public sealed record DashboardKpiDto(
    decimal TotalOutstanding,
    decimal OutstandingInvoice,
    decimal OutstandingAdvance,
    decimal OverdueTotal,
    int OverdueCustomers,
    int OnTimeCustomers,
    decimal UnallocatedReceiptsAmount,
    int UnallocatedReceiptsCount,
    int PendingReceiptsCount,
    decimal PendingReceiptsAmount,
    int PendingAdvancesCount,
    decimal PendingAdvancesAmount,
    int PendingImportBatches,
    int LockedPeriodsCount);
