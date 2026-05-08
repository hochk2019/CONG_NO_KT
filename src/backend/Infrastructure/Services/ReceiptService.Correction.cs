using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.StatusCodes;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReceiptService
{
    public async Task<ReceiptDto> CorrectAsync(Guid receiptId, ReceiptCorrectionRequest request, CancellationToken ct)
    {
        _currentUser.EnsureUser();

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Correction reason is required.");
        }

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

        if (receipt.Status == ReceiptStatusCodes.Void)
        {
            throw new InvalidOperationException("Void receipts cannot be edited.");
        }

        await EnsureCanApproveReceipt(receipt, ct);

        if (receipt.Status is not (ReceiptStatusCodes.Draft or ReceiptStatusCodes.Approved))
        {
            throw new InvalidOperationException("Only draft or approved receipts can be edited.");
        }

        if (request.Amount <= 0)
        {
            throw new InvalidOperationException("Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.ReceiptNo))
        {
            throw new InvalidOperationException("Receipt number is required.");
        }

        var normalizedReceiptNo = request.ReceiptNo.Trim();
        var method = string.IsNullOrWhiteSpace(request.Method)
            ? receipt.Method
            : NormalizeMethod(request.Method);
        var allocationPriority = string.IsNullOrWhiteSpace(request.AllocationPriority)
            ? receipt.AllocationPriority
            : NormalizeAllocationPriority(request.AllocationPriority);
        var allocationMode = string.IsNullOrWhiteSpace(request.AllocationMode)
            ? NormalizeAllocationMode(receipt.AllocationMode)
            : NormalizeAllocationMode(request.AllocationMode);
        var appliedPeriodStart = request.AppliedPeriodStart;
        if (appliedPeriodStart.HasValue && appliedPeriodStart.Value.Day != 1)
        {
            appliedPeriodStart = new DateOnly(appliedPeriodStart.Value.Year, appliedPeriodStart.Value.Month, 1);
        }

        var selectedTargets = request.SelectedTargets is null
            ? DeserializeTargets(receipt.AllocationTargets)?.ToList() ?? []
            : NormalizeSelectedTargets(request.SelectedTargets).ToList();
        var now = DateTimeOffset.UtcNow;

        var allocationFieldsChanged =
            request.Amount != receipt.Amount ||
            !string.Equals(allocationMode, receipt.AllocationMode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(allocationPriority, receipt.AllocationPriority, StringComparison.OrdinalIgnoreCase) ||
            appliedPeriodStart != receipt.AppliedPeriodStart ||
            BuildTargetSignature(selectedTargets) != BuildTargetSignature(DeserializeTargets(receipt.AllocationTargets));

        if (receipt.Status == ReceiptStatusCodes.Approved && allocationFieldsChanged)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            await ReverseApprovedEffectsAsync(receipt, now, ct);
            await ApplyCorrectionDraftValuesAsync(
                receipt,
                normalizedReceiptNo,
                request,
                method,
                allocationMode,
                allocationPriority,
                appliedPeriodStart,
                selectedTargets,
                now,
                ct);

            receipt.Status = ReceiptStatusCodes.Draft;
            receipt.ApprovedAt = null;
            receipt.ApprovedBy = null;

            await DocumentDuplicateGuard.SaveReceiptChangesAsync(_db, ct);
            await tx.CommitAsync(ct);
        }
        else
        {
            await DocumentDuplicateGuard.EnsureReceiptNumberAvailableAsync(
                _db,
                receipt.SellerTaxCode,
                receipt.CustomerTaxCode,
                normalizedReceiptNo,
                receipt.Id,
                ct);

            receipt.ReceiptNo = normalizedReceiptNo;
            receipt.ReceiptDate = request.ReceiptDate;
            receipt.Method = method;
            receipt.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

            if (receipt.Status == ReceiptStatusCodes.Draft)
            {
                await ApplyCorrectionDraftValuesAsync(
                    receipt,
                    normalizedReceiptNo,
                    request,
                    method,
                    allocationMode,
                    allocationPriority,
                    appliedPeriodStart,
                    selectedTargets,
                    now,
                    ct,
                ensureDuplicate: false);
            }

            receipt.UpdatedAt = now;
            receipt.Version += 1;

            await DocumentDuplicateGuard.SaveReceiptChangesAsync(_db, ct);
        }

        await _auditService.LogAsync(
            "RECEIPT_CORRECT",
            "Receipt",
            receipt.Id.ToString(),
            new
            {
                receiptId,
                PreviousStatus = receipt.Status == ReceiptStatusCodes.Draft && allocationFieldsChanged
                    ? ReceiptStatusCodes.Approved
                    : receipt.Status
            },
            new
            {
                receipt.ReceiptNo,
                receipt.ReceiptDate,
                receipt.Amount,
                receipt.Method,
                receipt.Description,
                receipt.Status,
                receipt.AllocationMode,
                receipt.AllocationStatus,
                receipt.AllocationPriority,
                request.Reason
            },
            ct);

        return MapReceiptDto(receipt);
    }

    private async Task ApplyCorrectionDraftValuesAsync(
        Receipt receipt,
        string normalizedReceiptNo,
        ReceiptCorrectionRequest request,
        string method,
        string allocationMode,
        string allocationPriority,
        DateOnly? appliedPeriodStart,
        IReadOnlyList<ReceiptTargetRef> selectedTargets,
        DateTimeOffset now,
        CancellationToken ct,
        bool ensureDuplicate = true)
    {
        if (ensureDuplicate)
        {
            await DocumentDuplicateGuard.EnsureReceiptNumberAvailableAsync(
                _db,
                receipt.SellerTaxCode,
                receipt.CustomerTaxCode,
                normalizedReceiptNo,
                receipt.Id,
                ct);
        }

        if (selectedTargets.Count > 0)
        {
            var openItems = await ListOpenItemsAsync(receipt.SellerTaxCode, receipt.CustomerTaxCode, ct);
            ValidateSelectedTargets(selectedTargets, openItems);
            allocationMode = "MANUAL";
            appliedPeriodStart = null;
        }
        else if (allocationMode == "BY_PERIOD" && appliedPeriodStart is null)
        {
            throw new InvalidOperationException("Applied period start is required for BY_PERIOD.");
        }

        receipt.ReceiptNo = normalizedReceiptNo;
        receipt.ReceiptDate = request.ReceiptDate;
        receipt.Amount = request.Amount;
        receipt.Method = method;
        receipt.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        receipt.AllocationMode = allocationMode;
        receipt.AppliedPeriodStart = appliedPeriodStart;
        receipt.AllocationPriority = allocationPriority;
        receipt.AllocationTargets = SerializeTargets(selectedTargets.Count > 0 ? selectedTargets : null);
        receipt.AllocationStatus = selectedTargets.Count > 0
            ? ReceiptAllocationStatusCodes.Selected
            : ReceiptAllocationStatusCodes.Unallocated;
        receipt.AllocationSource = selectedTargets.Count > 0 ? "MANUAL" : null;
        receipt.UnallocatedAmount = 0;
        receipt.UpdatedAt = now;
        receipt.Version += 1;
    }

    private async Task ReverseApprovedEffectsAsync(Receipt receipt, DateTimeOffset now, CancellationToken ct)
    {
        var allocations = await _db.ReceiptAllocations
            .Where(a => a.ReceiptId == receipt.Id)
            .ToListAsync(ct);
        var heldCredits = await _db.ReceiptHeldCredits
            .Where(item => item.ReceiptId == receipt.Id)
            .ToListAsync(ct);

        if (allocations.Count > 0)
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
                    RestoreInvoice(invoice, allocation.Amount, now);
                }

                if (allocation.AdvanceId.HasValue && advances.TryGetValue(allocation.AdvanceId.Value, out var advance))
                {
                    RestoreAdvance(advance, allocation.Amount, now);
                }
            }

            _db.ReceiptAllocations.RemoveRange(allocations);
        }

        if (heldCredits.Count > 0)
        {
            _db.ReceiptHeldCredits.RemoveRange(heldCredits);
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.TaxCode == receipt.CustomerTaxCode, ct);
        if (customer is not null)
        {
            AdjustCustomerBalance(customer, receipt.Amount, now);
        }
    }

    private static string BuildTargetSignature(IReadOnlyList<ReceiptTargetRef>? targets)
    {
        return string.Join(
            "|",
            (targets ?? [])
                .OrderBy(item => item.TargetType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id)
                .Select(item => $"{item.TargetType.ToUpperInvariant()}:{item.Id}"));
    }
}
