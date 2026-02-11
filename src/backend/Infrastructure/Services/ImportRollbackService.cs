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

        if (receipts.Any(r => r.Status == "APPROVED"))
        {
            throw new InvalidOperationException("Rollback blocked: batch receipts are approved. Void receipts first.");
        }

        if (receiptIds.Count > 0)
        {
            var hasReceiptAllocations = await _db.ReceiptAllocations
                .AnyAsync(a => receiptIds.Contains(a.ReceiptId), ct);
            if (hasReceiptAllocations)
            {
                throw new InvalidOperationException("Rollback blocked: batch receipts have allocations. Void receipts first.");
            }
        }

        if (invoiceIds.Count > 0)
        {
            var hasInvoiceAllocations = await _db.ReceiptAllocations
                .AnyAsync(a => a.InvoiceId.HasValue && invoiceIds.Contains(a.InvoiceId.Value), ct);
            if (hasInvoiceAllocations)
            {
                throw new InvalidOperationException("Rollback blocked: batch invoices are allocated. Void receipts first.");
            }
        }

        if (advanceIds.Count > 0)
        {
            var hasAdvanceAllocations = await _db.ReceiptAllocations
                .AnyAsync(a => a.AdvanceId.HasValue && advanceIds.Contains(a.AdvanceId.Value), ct);
            if (hasAdvanceAllocations)
            {
                throw new InvalidOperationException("Rollback blocked: batch advances are allocated. Void receipts first.");
            }
        }

        var lockedPeriods = await ImportRollbackPeriodLock.GetLockedPeriodsAsync(_db, invoices, advances, receipts, ct);
        var overrideApplied = false;
        var overrideReason = string.Empty;
        if (lockedPeriods.Count > 0)
        {
            if (!request.OverridePeriodLock)
            {
                throw new InvalidOperationException($"Period is locked for rollback: {string.Join(", ", lockedPeriods)}.");
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
}
