namespace CongNoGolden.Application.Invoices;

public sealed record InvoiceCreditReconcileResult(
    int InvoicesUpdated,
    int ReceiptsUpdated,
    int AllocationsCreated);
