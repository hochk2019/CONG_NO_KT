using CongNoGolden.Application.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReceiptService
{
    public async Task UpdateReminderAsync(Guid receiptId, ReceiptReminderUpdateRequest request, CancellationToken ct)
    {
        EnsureUser();

        var receipt = await _db.Receipts.FirstOrDefaultAsync(r => r.Id == receiptId && r.DeletedAt == null, ct);
        if (receipt is null)
        {
            throw new InvalidOperationException("Receipt not found.");
        }

        await EnsureCanApproveReceipt(receipt, ct);

        receipt.ReminderDisabledAt = request.Disabled ? DateTimeOffset.UtcNow : null;
        receipt.UpdatedAt = DateTimeOffset.UtcNow;
        receipt.Version += 1;

        await _db.SaveChangesAsync(ct);
    }
}
