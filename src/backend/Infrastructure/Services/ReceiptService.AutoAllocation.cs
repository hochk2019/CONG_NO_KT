using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.StatusCodes;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Domain.Allocation;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReceiptService
{
    public async Task<ReceiptDto> UpdateAutoAllocateAsync(
        Guid receiptId,
        ReceiptAutoAllocateUpdateRequest request,
        CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var receipt = await _db.Receipts.FirstOrDefaultAsync(r => r.Id == receiptId && r.DeletedAt == null, ct);
        if (receipt is null)
        {
            throw new InvalidOperationException("Receipt not found.");
        }

        await EnsureCanApproveReceipt(receipt, ct);

        if (request.Version is null)
        {
            throw new InvalidOperationException("Receipt version is required.");
        }

        if (request.Version.Value != receipt.Version)
        {
            throw new ConcurrencyException("Receipt was updated by another user. Please refresh.");
        }

        if (receipt.Status != ReceiptStatusCodes.Approved)
        {
            throw new InvalidOperationException("Only approved receipts can update auto allocation.");
        }

        if (receipt.UnallocatedAmount <= 0)
        {
            throw new InvalidOperationException("Receipt has no unallocated amount.");
        }

        if (receipt.AutoAllocateEnabled == request.AutoAllocateEnabled)
        {
            return MapReceiptDto(receipt);
        }

        var previousAutoAllocateEnabled = receipt.AutoAllocateEnabled;
        var previousUnallocatedAmount = receipt.UnallocatedAmount;
        var previousAllocationStatus = receipt.AllocationStatus;
        var now = DateTimeOffset.UtcNow;
        var allocatedTotal = 0m;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        receipt.AutoAllocateEnabled = request.AutoAllocateEnabled;

        if (request.AutoAllocateEnabled)
        {
            allocatedTotal = await ApplyApprovedReceiptAutoAllocationAsync(receipt, ct);
        }

        receipt.UpdatedAt = now;
        receipt.Version += 1;

        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            "RECEIPT_AUTO_ALLOCATE_UPDATE",
            "Receipt",
            receipt.Id.ToString(),
            new
            {
                autoAllocateEnabled = previousAutoAllocateEnabled,
                unallocatedAmount = previousUnallocatedAmount,
                allocationStatus = previousAllocationStatus
            },
            new
            {
                autoAllocateEnabled = receipt.AutoAllocateEnabled,
                allocatedTotal,
                unallocatedAmount = receipt.UnallocatedAmount,
                allocationStatus = receipt.AllocationStatus
            },
            ct);

        await tx.CommitAsync(ct);

        return MapReceiptDto(receipt);
    }

    public async Task<ReceiptPreviewResult> AllocateApprovedAsync(
        Guid receiptId,
        ReceiptApprovedAllocationRequest request,
        CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var receipt = await _db.Receipts.FirstOrDefaultAsync(r => r.Id == receiptId && r.DeletedAt == null, ct);
        if (receipt is null)
        {
            throw new InvalidOperationException("Receipt not found.");
        }

        await EnsureCanApproveReceipt(receipt, ct);

        if (request.Version is null)
        {
            throw new InvalidOperationException("Receipt version is required.");
        }

        if (request.Version.Value != receipt.Version)
        {
            throw new ConcurrencyException("Receipt was updated by another user. Please refresh.");
        }

        if (receipt.Status != ReceiptStatusCodes.Approved)
        {
            throw new InvalidOperationException("Only approved receipts can be allocated manually.");
        }

        if (receipt.AutoAllocateEnabled)
        {
            throw new InvalidOperationException("Disable auto allocation before allocating this receipt manually.");
        }

        if (receipt.UnallocatedAmount <= 0)
        {
            throw new InvalidOperationException("Receipt has no unallocated amount.");
        }

        var selectedTargets = NormalizeSelectedTargets(request.SelectedTargets);
        if (selectedTargets.Count == 0)
        {
            throw new InvalidOperationException("Cần chọn chứng từ để phân bổ phiếu thu.");
        }

        var lockedPeriods = await ReceiptPeriodLock.GetLockedPeriodsAsync(_db, receipt, ct);
        var overrideApplied = false;
        var overrideReason = string.Empty;
        if (lockedPeriods.Count > 0)
        {
            if (!request.OverridePeriodLock)
            {
                throw new InvalidOperationException(
                    $"Period is locked for receipt allocation: {string.Join(", ", lockedPeriods)}.");
            }

            overrideReason = PeriodLockOverridePolicy.RequireOverride(_currentUser, request.OverrideReason);
            overrideApplied = true;
        }

        var openItems = await LoadOpenItemsAsync(receipt.SellerTaxCode, receipt.CustomerTaxCode, ct);
        ValidateSelectedTargets(selectedTargets, openItems);

        var allocation = AllocationEngine.Allocate(
            new AllocationRequest(
                receipt.UnallocatedAmount,
                AllocationMode.Manual,
                receipt.AppliedPeriodStart,
                MapSelectedTargets(selectedTargets)),
            await LoadTargetsAsync(receipt.SellerTaxCode, receipt.CustomerTaxCode, ct));

        var allocatedTotal = allocation.Lines.Sum(line => line.Amount);
        if (allocatedTotal <= 0)
        {
            throw new InvalidOperationException("No amount could be allocated to the selected documents.");
        }

        var previousUnallocatedAmount = receipt.UnallocatedAmount;
        var previousAllocationStatus = receipt.AllocationStatus;
        var now = DateTimeOffset.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        await ApplyAllocations(receipt, allocation.Lines, ct);

        receipt.UnallocatedAmount = allocation.UnallocatedAmount;
        receipt.AllocationMode = "MANUAL";
        receipt.AllocationStatus = allocation.UnallocatedAmount > 0
            ? ReceiptAllocationStatusCodes.Partial
            : ReceiptAllocationStatusCodes.Allocated;
        receipt.AllocationSource = "MANUAL";
        receipt.AllocationTargets = SerializeTargets(MergeSelectedTargets(
            DeserializeTargets(receipt.AllocationTargets),
            selectedTargets));
        receipt.UpdatedAt = now;
        receipt.Version += 1;

        await _db.SaveChangesAsync(ct);

        if (overrideApplied)
        {
            await _auditService.LogAsync(
                "PERIOD_LOCK_OVERRIDE",
                "Receipt",
                receipt.Id.ToString(),
                null,
                new { operation = "RECEIPT_ALLOCATE_APPROVED", lockedPeriods, reason = overrideReason },
                ct);
        }

        await _auditService.LogAsync(
            "RECEIPT_ALLOCATE_APPROVED",
            "Receipt",
            receipt.Id.ToString(),
            new
            {
                unallocatedAmount = previousUnallocatedAmount,
                allocationStatus = previousAllocationStatus
            },
            new
            {
                allocatedTotal,
                unallocatedAmount = receipt.UnallocatedAmount,
                allocationStatus = receipt.AllocationStatus
            },
            ct);

        await tx.CommitAsync(ct);

        return new ReceiptPreviewResult(
            allocation.Lines.Select(line => new ReceiptPreviewLine(
                line.TargetId,
                line.TargetType.ToString().ToUpperInvariant(),
                line.Amount)).ToList(),
            allocation.UnallocatedAmount);
    }

    private static IReadOnlyList<ReceiptTargetRef> MergeSelectedTargets(
        IReadOnlyList<ReceiptTargetRef>? existing,
        IReadOnlyList<ReceiptTargetRef> appended)
    {
        if ((existing is null || existing.Count == 0) && appended.Count == 0)
        {
            return Array.Empty<ReceiptTargetRef>();
        }

        return (existing ?? Array.Empty<ReceiptTargetRef>())
            .Concat(appended)
            .GroupBy(item => (item.TargetType.ToUpperInvariant(), item.Id))
            .Select(group => group.First())
            .ToList();
    }

    private async Task<decimal> ApplyApprovedReceiptAutoAllocationAsync(
        Receipt receipt,
        CancellationToken ct)
    {
        if (receipt.UnallocatedAmount <= 0)
        {
            return 0m;
        }

        var orderedTargets = await LoadAutoAllocationTargetsAsync(
            receipt.SellerTaxCode,
            receipt.CustomerTaxCode,
            ct);

        if (orderedTargets.Count == 0)
        {
            return 0m;
        }

        var allocation = AllocationEngine.Allocate(
            new AllocationRequest(
                receipt.UnallocatedAmount,
                AllocationMode.Manual,
                receipt.AppliedPeriodStart,
                orderedTargets.Select(target => new AllocationTargetRef(target.Id, target.TargetType)).ToList()),
            orderedTargets);

        if (allocation.Lines.Count == 0)
        {
            return 0m;
        }

        await ApplyAllocations(receipt, allocation.Lines, ct);

        receipt.UnallocatedAmount = allocation.UnallocatedAmount;
        receipt.AllocationStatus = allocation.UnallocatedAmount > 0
            ? ReceiptAllocationStatusCodes.Partial
            : ReceiptAllocationStatusCodes.Allocated;

        return allocation.Lines.Sum(line => line.Amount);
    }

    private async Task<List<AllocationTarget>> LoadAutoAllocationTargetsAsync(
        string sellerTaxCode,
        string customerTaxCode,
        CancellationToken ct)
    {
        var invoiceTargets = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.SellerTaxCode == sellerTaxCode && i.CustomerTaxCode == customerTaxCode && i.DeletedAt == null)
            .Where(i => i.OutstandingAmount > 0 && i.Status != "VOID")
            .OrderBy(i => i.IssueDate)
            .ThenBy(i => i.CreatedAt)
            .Select(i => new AllocationTarget(i.Id, AllocationTargetType.Invoice, i.IssueDate, i.OutstandingAmount))
            .ToListAsync(ct);

        var advanceTargets = await _db.Advances
            .AsNoTracking()
            .Where(a => a.SellerTaxCode == sellerTaxCode && a.CustomerTaxCode == customerTaxCode && a.DeletedAt == null)
            .Where(a => a.OutstandingAmount > 0 && (a.Status == "APPROVED" || a.Status == "PAID"))
            .OrderBy(a => a.AdvanceDate)
            .ThenBy(a => a.CreatedAt)
            .Select(a => new AllocationTarget(a.Id, AllocationTargetType.Advance, a.AdvanceDate, a.OutstandingAmount))
            .ToListAsync(ct);

        invoiceTargets.AddRange(advanceTargets);
        return invoiceTargets;
    }
}
