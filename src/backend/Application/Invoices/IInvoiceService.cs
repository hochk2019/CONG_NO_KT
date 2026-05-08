using CongNoGolden.Application.Common;

namespace CongNoGolden.Application.Invoices;

public interface IInvoiceService
{
    Task<PagedResult<InvoiceListItemDto>> ListAsync(InvoiceListRequest request, CancellationToken ct);
    Task<InvoiceVoidResult> VoidAsync(Guid invoiceId, InvoiceVoidRequest request, CancellationToken ct);
}
