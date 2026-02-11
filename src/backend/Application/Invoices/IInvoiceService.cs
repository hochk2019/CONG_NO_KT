namespace CongNoGolden.Application.Invoices;

public interface IInvoiceService
{
    Task<InvoiceVoidResult> VoidAsync(Guid invoiceId, InvoiceVoidRequest request, CancellationToken ct);
}
