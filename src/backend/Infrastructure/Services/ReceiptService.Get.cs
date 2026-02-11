using CongNoGolden.Application.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReceiptService
{
    public async Task<ReceiptDto> GetAsync(Guid receiptId, CancellationToken ct)
    {
        EnsureUser();

        var receipt = await _db.Receipts
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == receiptId && r.DeletedAt == null, ct);

        if (receipt is null)
        {
            throw new InvalidOperationException("Receipt not found.");
        }

        await EnsureCanApproveReceipt(receipt, ct);

        return new ReceiptDto(
            receipt.Id,
            receipt.Status,
            receipt.Version,
            receipt.Amount,
            receipt.UnallocatedAmount,
            receipt.ReceiptNo,
            receipt.ReceiptDate,
            receipt.AppliedPeriodStart,
            receipt.AllocationMode,
            receipt.AllocationStatus,
            receipt.AllocationPriority,
            receipt.AllocationSource,
            receipt.AllocationSuggestedAt,
            DeserializeTargets(receipt.AllocationTargets),
            receipt.Method,
            receipt.SellerTaxCode,
            receipt.CustomerTaxCode);
    }
}
