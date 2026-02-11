namespace CongNoGolden.Application.Invoices;

public interface IInvoiceCreditReconcileService
{
    Task<InvoiceCreditReconcileResult> RunAsync(CancellationToken ct);
}
