using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Imports;
using CongNoGolden.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed class ImportRollbackService : IImportRollbackService
{
    private const string StatusCommitted = "COMMITTED";
    private const string StatusRolledBack = "ROLLED_BACK";

    private readonly ConGNoDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private const int BlockingPreviewLimit = 10;

    public ImportRollbackService(ConGNoDbContext db, ICurrentUser currentUser, IAuditService auditService)
    {
        _db = db;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<ImportRollbackResult> RollbackAsync(Guid batchId, ImportRollbackRequest request, CancellationToken ct)
    {
        var batch = await _db.ImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null)
        {
            throw new InvalidOperationException("Batch not found.");
        }

        if (batch.Status != StatusCommitted)
        {
            throw new InvalidOperationException("Batch status is not eligible for rollback.");
        }

        var previousStatus = batch.Status;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var invoices = await _db.Invoices.Where(i => i.SourceBatchId == batchId && i.DeletedAt == null).ToListAsync(ct);
        var advances = await _db.Advances.Where(a => a.SourceBatchId == batchId && a.DeletedAt == null).ToListAsync(ct);
        var receipts = await _db.Receipts.Where(r => r.SourceBatchId == batchId && r.DeletedAt == null).ToListAsync(ct);

        var invoiceIds = invoices.Select(i => i.Id).ToList();
        var advanceIds = advances.Select(a => a.Id).ToList();
        var receiptIds = receipts.Select(r => r.Id).ToList();

        var approvedReceiptIds = receipts
            .Where(r => r.Status == "APPROVED")
            .Select(r => r.Id)
            .Take(BlockingPreviewLimit)
            .ToList();
        if (approvedReceiptIds.Count > 0)
        {
            var approvedReceiptCount = receipts.Count(r => r.Status == "APPROVED");
            throw CreateBlockedException(
                "RECEIPTS_APPROVED",
                batchId,
                "Rollback blocked: batch receipts are approved. Void receipts first.",
                new Dictionary<string, object?>
                {
                    ["approvedReceiptCount"] = approvedReceiptCount,
                    ["approvedReceiptIds"] = approvedReceiptIds
                });
        }

        if (receiptIds.Count > 0)
        {
            var receiptAllocationLinks = await _db.ReceiptAllocations
                .Where(a => receiptIds.Contains(a.ReceiptId))
                .Select(a => new { a.ReceiptId, a.InvoiceId, a.AdvanceId })
                .Take(BlockingPreviewLimit)
                .ToListAsync(ct);
            if (receiptAllocationLinks.Count > 0)
            {
                throw CreateBlockedException(
                    "RECEIPTS_ALLOCATED",
                    batchId,
                    "Rollback blocked: batch receipts have allocations. Void receipts first.",
                    new Dictionary<string, object?>
                    {
                        ["receiptAllocationCount"] = receiptAllocationLinks.Count,
                        ["receiptIds"] = receiptAllocationLinks.Select(a => a.ReceiptId).Distinct().ToList(),
                        ["invoiceIds"] = receiptAllocationLinks.Where(a => a.InvoiceId.HasValue).Select(a => a.InvoiceId!.Value).Distinct().ToList(),
                        ["advanceIds"] = receiptAllocationLinks.Where(a => a.AdvanceId.HasValue).Select(a => a.AdvanceId!.Value).Distinct().ToList()
                    });
            }
        }

        if (invoiceIds.Count > 0)
        {
            var invoiceAllocationLinks = await _db.ReceiptAllocations
                .Where(a => a.InvoiceId.HasValue && invoiceIds.Contains(a.InvoiceId.Value))
                .Select(a => new { a.InvoiceId, a.ReceiptId })
                .Take(BlockingPreviewLimit)
                .ToListAsync(ct);
            if (invoiceAllocationLinks.Count > 0)
            {
                throw CreateBlockedException(
                    "INVOICES_ALLOCATED",
                    batchId,
                    "Rollback blocked: batch invoices are allocated. Void receipts first.",
                    new Dictionary<string, object?>
                    {
                        ["invoiceAllocationCount"] = invoiceAllocationLinks.Count,
                        ["invoiceIds"] = invoiceAllocationLinks.Where(a => a.InvoiceId.HasValue).Select(a => a.InvoiceId!.Value).Distinct().ToList(),
                        ["receiptIds"] = invoiceAllocationLinks.Select(a => a.ReceiptId).Distinct().ToList()
                    });
            }
        }

        if (advanceIds.Count > 0)
        {
            var advanceAllocationLinks = await _db.ReceiptAllocations
                .Where(a => a.AdvanceId.HasValue && advanceIds.Contains(a.AdvanceId.Value))
                .Select(a => new { a.AdvanceId, a.ReceiptId })
                .Take(BlockingPreviewLimit)
                .ToListAsync(ct);
            if (advanceAllocationLinks.Count > 0)
            {
                throw CreateBlockedException(
                    "ADVANCES_ALLOCATED",
                    batchId,
                    "Rollback blocked: batch advances are allocated. Void receipts first.",
                    new Dictionary<string, object?>
                    {
                        ["advanceAllocationCount"] = advanceAllocationLinks.Count,
                        ["advanceIds"] = advanceAllocationLinks.Where(a => a.AdvanceId.HasValue).Select(a => a.AdvanceId!.Value).Distinct().ToList(),
                        ["receiptIds"] = advanceAllocationLinks.Select(a => a.ReceiptId).Distinct().ToList()
                    });
            }
        }

        var lockedPeriods = await ImportRollbackPeriodLock.GetLockedPeriodsAsync(_db, invoices, advances, receipts, ct);
        var overrideApplied = false;
        var overrideReason = string.Empty;
        if (lockedPeriods.Count > 0)
        {
            if (!request.OverridePeriodLock)
            {
                throw CreateBlockedException(
                    "PERIOD_LOCKED",
                    batchId,
                    $"Period is locked for rollback: {string.Join(", ", lockedPeriods)}.",
                    new Dictionary<string, object?>
                    {
                        ["lockedPeriods"] = lockedPeriods
                    });
            }

            overrideReason = PeriodLockOverridePolicy.RequireOverride(_currentUser, request.OverrideReason);
            overrideApplied = true;
        }

        foreach (var invoice in invoices)
        {
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.TaxCode == invoice.CustomerTaxCode, ct);
            if (customer is not null)
            {
                customer.CurrentBalance -= invoice.TotalAmount;
            }

            invoice.DeletedAt = DateTimeOffset.UtcNow;
            invoice.DeletedBy = _currentUser.UserId;
        }

        foreach (var advance in advances)
        {
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.TaxCode == advance.CustomerTaxCode, ct);
            if (customer is not null)
            {
                customer.CurrentBalance -= advance.Amount;
            }

            advance.DeletedAt = DateTimeOffset.UtcNow;
            advance.DeletedBy = _currentUser.UserId;
        }

        foreach (var receipt in receipts)
        {
            receipt.DeletedAt = DateTimeOffset.UtcNow;
            receipt.DeletedBy = _currentUser.UserId;
        }

        batch.Status = StatusRolledBack;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (overrideApplied)
        {
            await _auditService.LogAsync(
                "PERIOD_LOCK_OVERRIDE",
                "ImportBatch",
                batch.Id.ToString(),
                null,
                new { operation = "IMPORT_ROLLBACK", lockedPeriods, reason = overrideReason },
                ct);
        }

        await _auditService.LogAsync(
            "IMPORT_ROLLBACK",
            "ImportBatch",
            batch.Id.ToString(),
            new { status = previousStatus },
            new { status = batch.Status, invoices = invoices.Count, advances = advances.Count, receipts = receipts.Count },
            ct);

        return new ImportRollbackResult(invoices.Count, advances.Count, receipts.Count);
    }

    private static ImportRollbackBlockedException CreateBlockedException(
        string reason,
        Guid batchId,
        string detail,
        Dictionary<string, object?> data)
    {
        var payload = new Dictionary<string, object?>(data)
        {
            ["action"] = "Xử lý các chứng từ liên quan trước rồi thực hiện hoàn tác lại."
        };
        return new ImportRollbackBlockedException(reason, detail, batchId, payload);
    }
}
