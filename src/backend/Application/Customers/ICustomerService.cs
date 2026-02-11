using CongNoGolden.Application.Common;

namespace CongNoGolden.Application.Customers;

public interface ICustomerService
{
    Task<PagedResult<CustomerListItem>> ListAsync(CustomerListRequest request, CancellationToken ct);
    Task<CustomerDetailDto?> GetAsync(string taxCode, CancellationToken ct);
    Task<PagedResult<CustomerInvoiceDto>> ListInvoicesAsync(string taxCode, CustomerRelationRequest request, CancellationToken ct);
    Task<PagedResult<CustomerAdvanceDto>> ListAdvancesAsync(string taxCode, CustomerRelationRequest request, CancellationToken ct);
    Task<PagedResult<CustomerReceiptDto>> ListReceiptsAsync(string taxCode, CustomerRelationRequest request, CancellationToken ct);
}
