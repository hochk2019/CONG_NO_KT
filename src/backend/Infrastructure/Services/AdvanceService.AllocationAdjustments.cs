using CongNoGolden.Application.Common.StatusCodes;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class AdvanceService
{
    private async Task<decimal> ReduceAdvanceAllocationsAsync(
        Advance advance,
        decimal nextAllocatedLimit,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var allocations = await _db.ReceiptAllocations
            .Where(a => a.AdvanceId == advance.Id)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(ct);

        var retainedTotal = allocations.Sum(a => a.Amount);
        if (retainedTotal <= nextAllocatedLimit)
        {
            return retainedTotal;
        }

        var allocatedByReceipt = allocations
            .GroupBy(a => a.ReceiptId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));
        var receiptIds = allocatedByReceipt.Keys.ToList();
        var receipts = await _db.Receipts
            .Where(r => receiptIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);

        var amountToRelease = retainedTotal - nextAllocatedLimit;
        var touchedReceiptIds = new HashSet<Guid>();

        foreach (var allocation in allocations)
        {
            if (amountToRelease <= 0)
            {
                break;
            }

            if (!receipts.TryGetValue(allocation.ReceiptId, out var receipt))
            {
                throw new InvalidOperationException($"Receipt {allocation.ReceiptId} not found for allocation rebalance.");
            }

            var released = Math.Min(allocation.Amount, amountToRelease);
            if (released <= 0)
            {
                continue;
            }

            if (released == allocation.Amount)
            {
                _db.ReceiptAllocations.Remove(allocation);
            }
            else
            {
                allocation.Amount -= released;
            }

            receipt.UnallocatedAmount += released;
            allocatedByReceipt[receipt.Id] -= released;
            retainedTotal -= released;
            amountToRelease -= released;
            touchedReceiptIds.Add(receipt.Id);
        }

        foreach (var receiptId in touchedReceiptIds)
        {
            var receipt = receipts[receiptId];
            var hasAllocations = allocatedByReceipt.TryGetValue(receiptId, out var allocatedAmount) && allocatedAmount > 0;

            receipt.AllocationStatus = ResolveReceiptAllocationStatus(receipt.UnallocatedAmount, hasAllocations);
            if (!hasAllocations)
            {
                receipt.AllocationSource = null;
            }

            receipt.UpdatedAt = now;
            receipt.Version += 1;
        }

        return retainedTotal;
    }

    private static string ResolveReceiptAllocationStatus(decimal unallocatedAmount, bool hasAllocations)
    {
        if (unallocatedAmount <= 0)
        {
            return ReceiptAllocationStatusCodes.Allocated;
        }

        return hasAllocations
            ? ReceiptAllocationStatusCodes.Partial
            : ReceiptAllocationStatusCodes.Unallocated;
    }
}
