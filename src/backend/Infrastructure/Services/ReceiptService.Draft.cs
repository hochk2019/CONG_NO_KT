using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.StatusCodes;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReceiptService
{
    public async Task<ReceiptDto> UpdateDraftAsync(Guid receiptId, ReceiptDraftUpdateRequest request, CancellationToken ct)
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

        if (receipt.Status != ReceiptStatusCodes.Draft)
        {
            throw new InvalidOperationException("Only draft receipts can be edited.");
        }

        if (request.Amount <= 0)
        {
            throw new InvalidOperationException("Amount must be greater than zero.");
        }

        var method = string.IsNullOrWhiteSpace(request.Method)
            ? receipt.Method
            : NormalizeMethod(request.Method);

        var allocationPriority = string.IsNullOrWhiteSpace(request.AllocationPriority)
            ? receipt.AllocationPriority
            : NormalizeAllocationPriority(request.AllocationPriority);

        var requestedMode = string.IsNullOrWhiteSpace(request.AllocationMode)
            ? receipt.AllocationMode
            : request.AllocationMode;
        var allocationMode = NormalizeAllocationMode(requestedMode);
        var appliedPeriodStart = request.AppliedPeriodStart;

        var selectedTargets = request.SelectedTargets is null
            ? DeserializeTargets(receipt.AllocationTargets)?.ToList() ?? []
            : NormalizeSelectedTargets(request.SelectedTargets).ToList();

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

        var previous = new
        {
            receipt.ReceiptNo,
            receipt.ReceiptDate,
            receipt.Amount,
            receipt.AllocationMode,
            receipt.AppliedPeriodStart,
            receipt.AllocationPriority,
            receipt.Method,
            receipt.Status
        };

        var allocationStatus = selectedTargets.Count > 0
            ? ReceiptAllocationStatusCodes.Selected
            : ReceiptAllocationStatusCodes.Unallocated;
        var allocationSource = selectedTargets.Count > 0 ? "MANUAL" : null;
        var normalizedReceiptNo = string.IsNullOrWhiteSpace(request.ReceiptNo) ? null : request.ReceiptNo.Trim();

        receipt.ReceiptNo = normalizedReceiptNo;
        receipt.ReceiptDate = request.ReceiptDate;
        receipt.Amount = request.Amount;
        receipt.Method = method;
        receipt.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        receipt.AllocationMode = allocationMode;
        receipt.AppliedPeriodStart = appliedPeriodStart;
        receipt.AllocationPriority = allocationPriority;
        receipt.AllocationTargets = SerializeTargets(selectedTargets.Count > 0 ? selectedTargets : null);
        receipt.AllocationStatus = allocationStatus;
        receipt.AllocationSource = allocationSource;
        receipt.UnallocatedAmount = 0;
        receipt.UpdatedAt = DateTimeOffset.UtcNow;
        receipt.Version += 1;

        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            "RECEIPT_UPDATE_DRAFT",
            "Receipt",
            receipt.Id.ToString(),
            previous,
            new
            {
                receipt.ReceiptNo,
                receipt.ReceiptDate,
                receipt.Amount,
                receipt.AllocationMode,
                receipt.AppliedPeriodStart,
                receipt.AllocationPriority,
                receipt.Method,
                receipt.Status
            },
            ct);

        return MapReceiptDto(receipt);
    }

    public async Task<ReceiptBulkApproveResult> ApproveBulkAsync(ReceiptBulkApproveRequest request, CancellationToken ct)
    {
        _currentUser.EnsureUser();

        if (request.Items is null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("At least one receipt is required for bulk approval.");
        }

        var approved = 0;
        var failed = 0;
        var itemResults = new List<ReceiptBulkApproveItemResult>();

        foreach (var item in request.Items)
        {
            try
            {
                var preview = await ApproveAsync(
                    item.ReceiptId,
                    new ReceiptApproveRequest(
                        item.SelectedTargets,
                        item.Version,
                        item.OverridePeriodLock,
                        item.OverrideReason),
                    ct);

                approved += 1;
                itemResults.Add(new ReceiptBulkApproveItemResult(
                    item.ReceiptId,
                    "APPROVED",
                    preview,
                    null,
                    null));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or ConcurrencyException)
            {
                failed += 1;
                itemResults.Add(new ReceiptBulkApproveItemResult(
                    item.ReceiptId,
                    "FAILED",
                    null,
                    ResolveBulkErrorCode(ex),
                    ex.Message));

                if (!request.ContinueOnError)
                {
                    break;
                }
            }
        }

        return new ReceiptBulkApproveResult(request.Items.Count, approved, failed, itemResults);
    }

    private static ReceiptDto MapReceiptDto(Receipt receipt)
    {
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

    private static string ResolveBulkErrorCode(Exception ex)
    {
        return ex switch
        {
            ConcurrencyException => "CONFLICT",
            UnauthorizedAccessException => "FORBIDDEN",
            InvalidOperationException => "INVALID_OPERATION",
            _ => "UNKNOWN"
        };
    }
}
