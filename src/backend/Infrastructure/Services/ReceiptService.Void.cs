using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.StatusCodes;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReceiptService
{
    public async Task<ReceiptVoidResult> VoidAsync(Guid receiptId, ReceiptVoidRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Void reason is required.");
        }

        var receipt = await _db.Receipts.FirstOrDefaultAsync(r => r.Id == receiptId && r.DeletedAt == null, ct);
        if (receipt is null)
        {
            throw new InvalidOperationException("Receipt not found.");
        }

        if (request.Version is null)
        {
            throw new InvalidOperationException("Receipt version is required.");
        }

        if (request.Version.Value != receipt.Version)
        {
            throw new ConcurrencyException("Receipt was updated by another user. Please refresh.");
        }

        if (receipt.Status == ReceiptStatusCodes.Void)
        {
            throw new InvalidOperationException("Receipt already voided.");
        }

        await EnsureCanApproveReceipt(receipt, ct);

        var lockedPeriods = await ReceiptPeriodLock.GetLockedPeriodsAsync(_db, receipt, ct);
        var overrideApplied = false;
        var overrideReason = string.Empty;
        if (lockedPeriods.Count > 0)
        {
            if (!request.OverridePeriodLock)
            {
                throw new InvalidOperationException(
                    $"Period is locked for receipt void: {string.Join(", ", lockedPeriods)}.");
            }

            overrideReason = PeriodLockOverridePolicy.RequireOverride(_currentUser, request.OverrideReason);
            overrideApplied = true;
        }

        var previousStatus = receipt.Status;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var allocations = await _db.ReceiptAllocations
            .Where(a => a.ReceiptId == receipt.Id)
            .ToListAsync(ct);

        var reversedAmount = allocations.Sum(a => a.Amount);
        var allocationCount = allocations.Count;

        if (allocationCount > 0)
        {
            var invoiceIds = allocations
                .Where(a => a.InvoiceId.HasValue)
                .Select(a => a.InvoiceId!.Value)
                .Distinct()
                .ToList();

            var advanceIds = allocations
                .Where(a => a.AdvanceId.HasValue)
                .Select(a => a.AdvanceId!.Value)
                .Distinct()
                .ToList();

            var invoices = invoiceIds.Count == 0
                ? new Dictionary<Guid, Invoice>()
                : await _db.Invoices.Where(i => invoiceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);

            var advances = advanceIds.Count == 0
                ? new Dictionary<Guid, Advance>()
                : await _db.Advances.Where(a => advanceIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);

            foreach (var allocation in allocations)
            {
                if (allocation.InvoiceId.HasValue && invoices.TryGetValue(allocation.InvoiceId.Value, out var invoice))
                {
                    RestoreInvoice(invoice, allocation.Amount);
                }

                if (allocation.AdvanceId.HasValue && advances.TryGetValue(allocation.AdvanceId.Value, out var advance))
                {
                    RestoreAdvance(advance, allocation.Amount);
                }
            }

            _db.ReceiptAllocations.RemoveRange(allocations);
        }

        if (previousStatus == ReceiptStatusCodes.Approved)
        {
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.TaxCode == receipt.CustomerTaxCode, ct);
            if (customer is not null)
            {
                customer.CurrentBalance += receipt.Amount;
            }
        }

        receipt.Status = ReceiptStatusCodes.Void;
        receipt.UnallocatedAmount = 0;
        receipt.AllocationStatus = "VOID";
        receipt.DeletedAt = DateTimeOffset.UtcNow;
        receipt.DeletedBy = _currentUser.UserId;
        receipt.UpdatedAt = DateTimeOffset.UtcNow;
        receipt.Version += 1;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (overrideApplied)
        {
            await _auditService.LogAsync(
                "PERIOD_LOCK_OVERRIDE",
                "Receipt",
                receipt.Id.ToString(),
                null,
                new { operation = "RECEIPT_VOID", lockedPeriods, reason = overrideReason },
                ct);
        }

        await _auditService.LogAsync(
            "RECEIPT_VOID",
            "Receipt",
            receipt.Id.ToString(),
            new { status = previousStatus },
            new { status = receipt.Status, reason = request.Reason, reversedAmount, allocationCount },
            ct);

        return new ReceiptVoidResult(reversedAmount, allocationCount);
    }

    public async Task<ReceiptDto> UnvoidAsync(Guid receiptId, ReceiptUnvoidRequest request, CancellationToken ct)
    {
        var receipt = await _db.Receipts.FirstOrDefaultAsync(r => r.Id == receiptId, ct);
        if (receipt is null)
        {
            throw new InvalidOperationException("Receipt not found.");
        }

        if (request.Version is null)
        {
            throw new InvalidOperationException("Receipt version is required.");
        }

        if (request.Version.Value != receipt.Version)
        {
            throw new ConcurrencyException("Receipt was updated by another user. Please refresh.");
        }

        if (receipt.Status != ReceiptStatusCodes.Void)
        {
            throw new InvalidOperationException("Receipt is not voided.");
        }

        await EnsureCanApproveReceipt(receipt, ct);

        var lockedPeriods = await ReceiptPeriodLock.GetLockedPeriodsAsync(_db, receipt, ct);
        var overrideApplied = false;
        var overrideReason = string.Empty;
        if (lockedPeriods.Count > 0)
        {
            if (!request.OverridePeriodLock)
            {
                throw new InvalidOperationException(
                    $"Period is locked for receipt unvoid: {string.Join(", ", lockedPeriods)}.");
            }

            overrideReason = PeriodLockOverridePolicy.RequireOverride(_currentUser, request.OverrideReason);
            overrideApplied = true;
        }

        var hasAllocations = await _db.ReceiptAllocations.AnyAsync(a => a.ReceiptId == receipt.Id, ct);
        if (hasAllocations)
        {
            throw new InvalidOperationException("Receipt has allocations and cannot be unvoided.");
        }

        var previousStatus = receipt.Status;
        var hasSelectedTargets = DeserializeTargets(receipt.AllocationTargets)?.Count > 0;

        receipt.Status = ReceiptStatusCodes.Draft;
        receipt.UnallocatedAmount = 0;
        receipt.AllocationStatus = hasSelectedTargets
            ? ReceiptAllocationStatusCodes.Selected
            : ReceiptAllocationStatusCodes.Unallocated;
        receipt.AllocationSource = hasSelectedTargets ? "MANUAL" : null;
        receipt.DeletedAt = null;
        receipt.DeletedBy = null;
        receipt.UpdatedAt = DateTimeOffset.UtcNow;
        receipt.Version += 1;

        await _db.SaveChangesAsync(ct);

        if (overrideApplied)
        {
            await _auditService.LogAsync(
                "PERIOD_LOCK_OVERRIDE",
                "Receipt",
                receipt.Id.ToString(),
                null,
                new { operation = "RECEIPT_UNVOID", lockedPeriods, reason = overrideReason },
                ct);
        }

        await _auditService.LogAsync(
            "RECEIPT_UNVOID",
            "Receipt",
            receipt.Id.ToString(),
            new { status = previousStatus },
            new { status = receipt.Status },
            ct);

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

    private static void RestoreInvoice(Invoice invoice, decimal amount)
    {
        invoice.OutstandingAmount = Math.Min(invoice.TotalAmount, invoice.OutstandingAmount + amount);
        if (invoice.OutstandingAmount <= 0)
        {
            invoice.Status = "PAID";
        }
        else if (invoice.OutstandingAmount >= invoice.TotalAmount)
        {
            invoice.Status = "OPEN";
        }
        else
        {
            invoice.Status = "PARTIAL";
        }
    }

    private static void RestoreAdvance(Advance advance, decimal amount)
    {
        advance.OutstandingAmount = Math.Min(advance.Amount, advance.OutstandingAmount + amount);
        advance.Status = advance.OutstandingAmount == 0 ? "PAID" : "APPROVED";
    }
}
