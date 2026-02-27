using System.Text.Json;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Common.StatusCodes;
using CongNoGolden.Application.Imports;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed class ImportCommitService : IImportCommitService
{
    private readonly ConGNoDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public ImportCommitService(ConGNoDbContext db, ICurrentUser currentUser, IAuditService auditService)
    {
        _db = db;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<ImportCommitResult> CommitAsync(Guid batchId, ImportCommitRequest request, CancellationToken ct)
    {
        var batch = await _db.ImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null)
        {
            throw new InvalidOperationException("Batch not found.");
        }

        if (batch.Status == ImportBatchStatusCodes.Committed)
        {
            var committedSummary = ParseSummary(batch.SummaryData);
            RecordImportCommitMetrics(batch.Type, committedSummary);
            return committedSummary;
        }

        if (batch.Status != ImportBatchStatusCodes.Staging)
        {
            throw new InvalidOperationException("Batch status is not eligible for commit.");
        }

        var previousStatus = batch.Status;

        if (request.IdempotencyKey is not null)
        {
            if (batch.IdempotencyKey is not null && batch.IdempotencyKey != request.IdempotencyKey)
            {
                throw new InvalidOperationException("Idempotency key mismatch.");
            }
            batch.IdempotencyKey = request.IdempotencyKey;
        }

        var rows = await _db.ImportStagingRows
            .AsNoTracking()
            .Where(r => r.BatchId == batchId)
            .ToListAsync(ct);

        if (rows.Any(r => r.ValidationStatus == ImportStagingHelpers.StatusError))
        {
            throw new InvalidOperationException("Batch contains ERROR rows.");
        }

        var eligible = rows.Where(r => r.ActionSuggestion != "SKIP").ToList();
        var progressSteps = new List<ImportCommitProgressStep>
        {
            new(
                "VALIDATION",
                15,
                eligible.Count,
                rows.Count,
                $"Validated {rows.Count} staging rows; {eligible.Count} eligible for commit.")
        };

        if (eligible.Count == 0)
        {
            progressSteps.Add(new ImportCommitProgressStep(
                "FINALIZE",
                100,
                0,
                0,
                "No eligible rows to commit."));

            var emptySummary = new ImportCommitResult(0, 0, 0, 0, 0, 0, progressSteps);
            batch.Status = ImportBatchStatusCodes.Committed;
            batch.CommittedAt = DateTimeOffset.UtcNow;
            batch.ApprovedBy = _currentUser.UserId;
            batch.ApprovedAt = DateTimeOffset.UtcNow;
            batch.SummaryData = JsonSerializer.Serialize(emptySummary);
            await _db.SaveChangesAsync(ct);
            await _auditService.LogAsync(
                "IMPORT_COMMIT",
                "ImportBatch",
                batch.Id.ToString(),
                new { status = previousStatus },
                new { status = batch.Status, summary = batch.SummaryData },
                ct);
            await NotifyImportCommittedAsync(batch, emptySummary, ct);
            RecordImportCommitMetrics(batch.Type, emptySummary);
            return emptySummary;
        }

        var commitRows = eligible;
        HashSet<InvoiceKey>? existingInvoiceKeys = null;
        if (batch.Type == "INVOICE")
        {
            existingInvoiceKeys = await LoadExistingInvoiceKeysAsync(eligible, ct);
            if (existingInvoiceKeys.Count > 0)
            {
                commitRows = eligible
                    .Select(row => new { row, key = TryBuildInvoiceKey(row.RawData) })
                    .Where(item => item.key is not null && !existingInvoiceKeys.Contains(item.key.Value))
                    .Select(item => item.row)
                    .ToList();
            }
        }

        var skippedRows = Math.Max(0, eligible.Count - commitRows.Count);
        progressSteps.Add(new ImportCommitProgressStep(
            "DEDUPE",
            30,
            commitRows.Count,
            eligible.Count,
            skippedRows > 0
                ? $"Skipped {skippedRows} duplicate rows."
                : "No duplicate rows detected."));

        if (commitRows.Count == 0)
        {
            progressSteps.Add(new ImportCommitProgressStep(
                "FINALIZE",
                100,
                0,
                0,
                "All eligible rows were deduplicated."));

            var dedupOnlySummary = new ImportCommitResult(
                0,
                0,
                0,
                eligible.Count,
                0,
                skippedRows,
                progressSteps);
            batch.Status = ImportBatchStatusCodes.Committed;
            batch.CommittedAt = DateTimeOffset.UtcNow;
            batch.ApprovedBy = _currentUser.UserId;
            batch.ApprovedAt = DateTimeOffset.UtcNow;
            batch.SummaryData = JsonSerializer.Serialize(dedupOnlySummary);
            await _db.SaveChangesAsync(ct);
            await _auditService.LogAsync(
                "IMPORT_COMMIT",
                "ImportBatch",
                batch.Id.ToString(),
                new { status = previousStatus },
                new { status = batch.Status, summary = batch.SummaryData },
                ct);
            await NotifyImportCommittedAsync(batch, dedupOnlySummary, ct);
            RecordImportCommitMetrics(batch.Type, dedupOnlySummary);
            return dedupOnlySummary;
        }

        var lockedPeriods = await ImportCommitPeriodLock.GetLockedPeriodsAsync(_db, batch.Type, commitRows, ct);
        var overrideApplied = false;
        var overrideReason = string.Empty;
        if (lockedPeriods.Count > 0)
        {
            if (!request.OverridePeriodLock)
            {
                throw new InvalidOperationException($"Period is locked for commit: {string.Join(", ", lockedPeriods)}.");
            }

            overrideReason = PeriodLockOverridePolicy.RequireOverride(_currentUser, request.OverrideReason);
            overrideApplied = true;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var sellers = await _db.Sellers.AsNoTracking().Select(s => s.SellerTaxCode).ToListAsync(ct);
        var sellerSet = new HashSet<string>(sellers, StringComparer.OrdinalIgnoreCase);
        var customerCache = new Dictionary<string, Customer>(StringComparer.OrdinalIgnoreCase);

        var insertedInvoices = 0;
        var insertedAdvances = 0;
        var insertedReceipts = 0;
        var now = DateTimeOffset.UtcNow;
        var processedRows = 0;
        var totalCommitRows = commitRows.Count;
        var progressCheckpoint = Math.Max(1, totalCommitRows / 4);

        var seenInvoiceKeys = existingInvoiceKeys ?? new HashSet<InvoiceKey>();
        foreach (var row in commitRows)
        {
            using var doc = JsonDocument.Parse(row.RawData);
            var raw = doc.RootElement;

            if (batch.Type == "INVOICE")
            {
                if (seenInvoiceKeys.Count > 0 || existingInvoiceKeys is not null)
                {
                    var key = TryBuildInvoiceKey(raw);
                    if (key is null)
                    {
                        continue;
                    }
                    if (seenInvoiceKeys.Contains(key.Value))
                    {
                        continue;
                    }
                    seenInvoiceKeys.Add(key.Value);
                }

                var invoice = ImportCommitBuilders.BuildInvoice(raw, batch.Id, sellerSet);
                var customer = await ImportCommitCustomers.EnsureCustomer(_db, raw, customerCache, ct);
                if (customer is not null)
                {
                    customer.CurrentBalance += invoice.TotalAmount;
                }

                _db.Invoices.Add(invoice);
                insertedInvoices++;
                await ApplyReceiptCreditsToInvoiceAsync(invoice, now, ct);
            }
            else if (batch.Type == "ADVANCE")
            {
                var advance = ImportCommitBuilders.BuildAdvance(raw, batch.Id, sellerSet);
                advance.CreatedBy = _currentUser.UserId;
                advance.ApprovedBy = _currentUser.UserId;
                var customer = await ImportCommitCustomers.EnsureCustomer(_db, raw, customerCache, ct);
                if (customer is not null)
                {
                    customer.CurrentBalance += advance.Amount;
                }

                _db.Advances.Add(advance);
                insertedAdvances++;
                await ApplyReceiptCreditsToAdvanceAsync(advance, now, ct);
            }
            else if (batch.Type == "RECEIPT")
            {
                var receipt = ImportCommitBuilders.BuildReceipt(raw, batch.Id, sellerSet);
                receipt.CreatedBy = _currentUser.UserId;
                await ImportCommitCustomers.EnsureCustomer(_db, raw, customerCache, ct);
                _db.Receipts.Add(receipt);
                insertedReceipts++;
            }

            processedRows += 1;
            if (processedRows == totalCommitRows || processedRows % progressCheckpoint == 0)
            {
                var percent = 30 + (int)Math.Round((processedRows / (double)totalCommitRows) * 60, MidpointRounding.AwayFromZero);
                progressSteps.Add(new ImportCommitProgressStep(
                    "APPLY_ROWS",
                    Math.Clamp(percent, 31, 95),
                    processedRows,
                    totalCommitRows,
                    $"Committed {processedRows}/{totalCommitRows} rows."));
            }
        }

        batch.Status = ImportBatchStatusCodes.Committed;
        batch.CommittedAt = DateTimeOffset.UtcNow;
        batch.ApprovedBy = _currentUser.UserId;
        batch.ApprovedAt = DateTimeOffset.UtcNow;
        progressSteps.Add(new ImportCommitProgressStep(
            "FINALIZE",
            100,
            totalCommitRows,
            totalCommitRows,
            "Batch committed successfully."));

        var summary = new ImportCommitResult(
            insertedInvoices,
            insertedAdvances,
            insertedReceipts,
            eligible.Count,
            totalCommitRows,
            skippedRows,
            progressSteps);
        batch.SummaryData = JsonSerializer.Serialize(summary);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (overrideApplied)
        {
            await _auditService.LogAsync(
                "PERIOD_LOCK_OVERRIDE",
                "ImportBatch",
                batch.Id.ToString(),
                null,
                new { operation = "IMPORT_COMMIT", lockedPeriods, reason = overrideReason },
                ct);
        }

        await _auditService.LogAsync(
            "IMPORT_COMMIT",
            "ImportBatch",
            batch.Id.ToString(),
            new { status = previousStatus },
            new { status = batch.Status, summary },
            ct);

        await NotifyImportCommittedAsync(batch, summary, ct);
        RecordImportCommitMetrics(batch.Type, summary);

        return summary;
    }

    private async Task NotifyImportCommittedAsync(
        ImportBatch batch,
        ImportCommitResult summary,
        CancellationToken ct)
    {
        if (_currentUser.UserId is null)
        {
            return;
        }

        var userId = _currentUser.UserId.Value;
        var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, ct);
        if (!userExists)
        {
            return;
        }

        var preference = await _db.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (preference is not null && !preference.ReceiveNotifications)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var title = "Nhập liệu đã ghi sổ";
        var body = $"Đã ghi sổ: {summary.InsertedInvoices} hóa đơn, {summary.InsertedAdvances} khoản trả hộ, {summary.InsertedReceipts} phiếu thu.";
        var metadata = JsonSerializer.Serialize(new
        {
            batchId = batch.Id,
            batchType = batch.Type,
            summary.InsertedInvoices,
            summary.InsertedAdvances,
            summary.InsertedReceipts
        });

        _db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Body = body,
            Severity = "INFO",
            Source = "IMPORT",
            Metadata = metadata,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
    }

    private static InvoiceKey? TryBuildInvoiceKey(string rawData)
    {
        using var doc = JsonDocument.Parse(rawData);
        return TryBuildInvoiceKey(doc.RootElement);
    }

    private async Task<decimal> ApplyReceiptCreditsToInvoiceAsync(
        Invoice invoice,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (invoice.OutstandingAmount <= 0)
        {
            return 0m;
        }

        var receipts = await _db.Receipts
            .Where(r => r.DeletedAt == null && r.Status == "APPROVED")
            .Where(r => r.SellerTaxCode == invoice.SellerTaxCode && r.CustomerTaxCode == invoice.CustomerTaxCode)
            .Where(r => r.UnallocatedAmount > 0)
            .OrderBy(r => r.ReceiptDate)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(ct);

        if (receipts.Count == 0)
        {
            return 0m;
        }

        var remaining = invoice.OutstandingAmount;
        var allocatedTotal = 0m;

        foreach (var receipt in receipts)
        {
            if (remaining <= 0)
            {
                break;
            }

            var allocated = Math.Min(remaining, receipt.UnallocatedAmount);
            if (allocated <= 0)
            {
                continue;
            }

            receipt.UnallocatedAmount -= allocated;
            receipt.AllocationStatus = receipt.UnallocatedAmount == 0 ? "ALLOCATED" : "PARTIAL";
            receipt.UpdatedAt = now;
            receipt.Version += 1;

            _db.ReceiptAllocations.Add(new ReceiptAllocation
            {
                Id = Guid.NewGuid(),
                ReceiptId = receipt.Id,
                TargetType = "INVOICE",
                InvoiceId = invoice.Id,
                Amount = allocated,
                CreatedAt = now
            });

            remaining -= allocated;
            allocatedTotal += allocated;
        }

        if (allocatedTotal > 0)
        {
            invoice.OutstandingAmount = remaining;
            invoice.Status = remaining == 0 ? "PAID" : "PARTIAL";
            invoice.UpdatedAt = now;
            invoice.Version += 1;
        }

        return allocatedTotal;
    }

    private async Task<decimal> ApplyReceiptCreditsToAdvanceAsync(
        Advance advance,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (advance.OutstandingAmount <= 0)
        {
            return 0m;
        }

        var receipts = await _db.Receipts
            .Where(r => r.DeletedAt == null && r.Status == "APPROVED")
            .Where(r => r.SellerTaxCode == advance.SellerTaxCode && r.CustomerTaxCode == advance.CustomerTaxCode)
            .Where(r => r.UnallocatedAmount > 0)
            .OrderBy(r => r.ReceiptDate)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(ct);

        if (receipts.Count == 0)
        {
            return 0m;
        }

        var remaining = advance.OutstandingAmount;
        var allocatedTotal = 0m;

        foreach (var receipt in receipts)
        {
            if (remaining <= 0)
            {
                break;
            }

            var allocated = Math.Min(remaining, receipt.UnallocatedAmount);
            if (allocated <= 0)
            {
                continue;
            }

            receipt.UnallocatedAmount -= allocated;
            receipt.AllocationStatus = receipt.UnallocatedAmount == 0 ? "ALLOCATED" : "PARTIAL";
            receipt.UpdatedAt = now;
            receipt.Version += 1;

            _db.ReceiptAllocations.Add(new ReceiptAllocation
            {
                Id = Guid.NewGuid(),
                ReceiptId = receipt.Id,
                TargetType = "ADVANCE",
                AdvanceId = advance.Id,
                Amount = allocated,
                CreatedAt = now
            });

            remaining -= allocated;
            allocatedTotal += allocated;
        }

        if (allocatedTotal > 0)
        {
            advance.OutstandingAmount = remaining;
            advance.Status = remaining == 0 ? "PAID" : "APPROVED";
            advance.UpdatedAt = now;
            advance.Version += 1;
        }

        return allocatedTotal;
    }

    private static InvoiceKey? TryBuildInvoiceKey(JsonElement raw)
    {
        var issueDate = ImportCommitJson.GetDate(raw, "issue_date");
        if (issueDate is null)
        {
            return null;
        }

        return new InvoiceKey(
            NormalizeKeyPart(ImportCommitJson.GetString(raw, "seller_tax_code")),
            NormalizeKeyPart(ImportCommitJson.GetString(raw, "customer_tax_code")),
            NormalizeKeyPart(ImportCommitJson.GetString(raw, "invoice_series")),
            NormalizeKeyPart(ImportCommitJson.GetString(raw, "invoice_no")),
            issueDate.Value);
    }

    private async Task<HashSet<InvoiceKey>> LoadExistingInvoiceKeysAsync(
        IReadOnlyList<ImportStagingRow> eligible,
        CancellationToken ct)
    {
        var keys = new List<InvoiceKey>();
        foreach (var row in eligible)
        {
            var key = TryBuildInvoiceKey(row.RawData);
            if (key is not null)
            {
                keys.Add(key.Value);
            }
        }

        if (keys.Count == 0)
        {
            return new HashSet<InvoiceKey>();
        }

        var sellerCodes = keys.Select(k => k.SellerTaxCode).Distinct().ToList();
        var customerCodes = keys.Select(k => k.CustomerTaxCode).Distinct().ToList();
        var invoiceNos = keys.Select(k => k.InvoiceNo).Distinct().ToList();
        var issueDates = keys.Select(k => k.IssueDate).Distinct().ToList();
        var seriesList = keys.Select(k => k.InvoiceSeries).Distinct().ToList();

        var existing = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.DeletedAt == null)
            .Where(i =>
                sellerCodes.Contains(i.SellerTaxCode) &&
                customerCodes.Contains(i.CustomerTaxCode) &&
                invoiceNos.Contains(i.InvoiceNo) &&
                issueDates.Contains(i.IssueDate) &&
                seriesList.Contains(i.InvoiceSeries ?? string.Empty))
            .Select(i => new
            {
                i.SellerTaxCode,
                i.CustomerTaxCode,
                i.InvoiceSeries,
                i.InvoiceNo,
                i.IssueDate
            })
            .ToListAsync(ct);

        return new HashSet<InvoiceKey>(existing.Select(i => new InvoiceKey(
            NormalizeKeyPart(i.SellerTaxCode),
            NormalizeKeyPart(i.CustomerTaxCode),
            NormalizeKeyPart(i.InvoiceSeries ?? string.Empty),
            NormalizeKeyPart(i.InvoiceNo),
            i.IssueDate)));
    }

    private static string NormalizeKeyPart(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private readonly record struct InvoiceKey(
        string SellerTaxCode,
        string CustomerTaxCode,
        string InvoiceSeries,
        string InvoiceNo,
        DateOnly IssueDate);

    private static ImportCommitResult ParseSummary(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return new ImportCommitResult(0, 0, 0);
        }

        try
        {
            var result = JsonSerializer.Deserialize<ImportCommitResult>(summary);
            return result ?? new ImportCommitResult(0, 0, 0);
        }
        catch
        {
            return new ImportCommitResult(0, 0, 0);
        }
    }

    private static void RecordImportCommitMetrics(string? batchType, ImportCommitResult summary)
    {
        BusinessMetrics.RecordImportCommit(
            batchType,
            summary.TotalEligibleRows,
            summary.CommittedRows,
            summary.SkippedRows);
    }
}
