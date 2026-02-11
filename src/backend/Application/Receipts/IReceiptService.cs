using CongNoGolden.Application.Common;

namespace CongNoGolden.Application.Receipts;

public interface IReceiptService
{
    Task<PagedResult<ReceiptListItem>> ListAsync(ReceiptListRequest request, CancellationToken ct);
    Task<ReceiptDto> GetAsync(Guid receiptId, CancellationToken ct);
    Task<ReceiptDto> CreateAsync(ReceiptCreateRequest request, CancellationToken ct);
    Task<IReadOnlyList<ReceiptOpenItemDto>> ListOpenItemsAsync(string sellerTaxCode, string customerTaxCode, CancellationToken ct);
    Task<ReceiptPreviewResult> PreviewAsync(ReceiptPreviewRequest request, CancellationToken ct);
    Task<ReceiptPreviewResult> ApproveAsync(Guid receiptId, ReceiptApproveRequest request, CancellationToken ct);
    Task<ReceiptVoidResult> VoidAsync(Guid receiptId, ReceiptVoidRequest request, CancellationToken ct);
    Task<IReadOnlyList<ReceiptAllocationDetailDto>> ListAllocationsAsync(Guid receiptId, CancellationToken ct);
    Task UpdateReminderAsync(Guid receiptId, ReceiptReminderUpdateRequest request, CancellationToken ct);
}
