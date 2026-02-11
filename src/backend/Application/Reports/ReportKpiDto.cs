namespace CongNoGolden.Application.Reports;

public sealed record ReportKpiDto(
    decimal TotalOutstanding,
    decimal OutstandingInvoice,
    decimal OutstandingAdvance,
    decimal UnallocatedReceiptsAmount,
    int UnallocatedReceiptsCount,
    decimal OverdueAmount,
    int OverdueCustomers,
    decimal DueSoonAmount,
    int DueSoonCustomers,
    int OnTimeCustomers
);
