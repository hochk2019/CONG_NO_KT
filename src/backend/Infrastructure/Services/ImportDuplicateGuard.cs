using System.Text.Json;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

internal static class ImportDuplicateGuard
{
    internal static async Task MarkDatabaseDuplicatesAsync(
        ConGNoDbContext db,
        string batchType,
        List<ImportStagingRow> rows,
        CancellationToken ct)
    {
        switch (batchType)
        {
            case "INVOICE":
                await MarkDatabaseDuplicatesAsync(
                    rows,
                    TryBuildInvoiceKey,
                    keys => LoadExistingInvoiceKeysAsync(db, keys, ct));
                break;
            case "ADVANCE":
                await MarkDatabaseDuplicatesAsync(
                    rows,
                    TryBuildAdvanceKey,
                    keys => LoadExistingAdvanceKeysAsync(db, keys, ct));
                break;
            case "RECEIPT":
                await MarkDatabaseDuplicatesAsync(
                    rows,
                    TryBuildReceiptKey,
                    keys => LoadExistingReceiptKeysAsync(db, keys, ct));
                break;
        }
    }

    internal static async Task<ImportCommitDeduplicationResult> FilterCommitRowsAsync(
        ConGNoDbContext db,
        string batchType,
        IReadOnlyList<ImportStagingRow> eligible,
        CancellationToken ct)
    {
        return batchType switch
        {
            "INVOICE" => await FilterCommitRowsAsync(
                eligible,
                TryBuildInvoiceKey,
                keys => LoadExistingInvoiceKeysAsync(db, keys, ct)),
            "ADVANCE" => await FilterCommitRowsAsync(
                eligible,
                TryBuildAdvanceKey,
                keys => LoadExistingAdvanceKeysAsync(db, keys, ct)),
            "RECEIPT" => await FilterCommitRowsAsync(
                eligible,
                TryBuildReceiptKey,
                keys => LoadExistingReceiptKeysAsync(db, keys, ct)),
            _ => new ImportCommitDeduplicationResult(eligible.ToList(), 0)
        };
    }

    private static async Task MarkDatabaseDuplicatesAsync<TKey>(
        List<ImportStagingRow> rows,
        Func<string, TKey?> tryBuildKey,
        Func<IReadOnlyList<TKey>, Task<HashSet<TKey>>> loadExistingKeys)
        where TKey : struct
    {
        var keys = rows
            .Select(row => tryBuildKey(row.RawData))
            .Where(key => key is not null)
            .Select(key => key!.Value)
            .Distinct()
            .ToList();

        if (keys.Count == 0)
        {
            return;
        }

        var existing = await loadExistingKeys(keys);
        if (existing.Count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            var key = tryBuildKey(row.RawData);
            if (key is null || !existing.Contains(key.Value))
            {
                continue;
            }

            var messages = ParseMessages(row.ValidationMessages);
            if (!messages.Contains("DUP_IN_DB"))
            {
                messages.Add("DUP_IN_DB");
            }

            row.ValidationMessages = JsonSerializer.Serialize(messages);
            row.ValidationStatus = ImportStagingHelpers.GetStatus(messages);
            row.ActionSuggestion = "SKIP";
        }
    }

    private static async Task<ImportCommitDeduplicationResult> FilterCommitRowsAsync<TKey>(
        IReadOnlyList<ImportStagingRow> eligible,
        Func<string, TKey?> tryBuildKey,
        Func<IReadOnlyList<TKey>, Task<HashSet<TKey>>> loadExistingKeys)
        where TKey : struct
    {
        var keys = eligible
            .Select(row => tryBuildKey(row.RawData))
            .Where(key => key is not null)
            .Select(key => key!.Value)
            .Distinct()
            .ToList();

        if (keys.Count == 0)
        {
            return new ImportCommitDeduplicationResult(eligible.ToList(), 0);
        }

        var seen = await loadExistingKeys(keys);
        var commitRows = new List<ImportStagingRow>(eligible.Count);
        var skippedRows = 0;

        foreach (var row in eligible)
        {
            var key = tryBuildKey(row.RawData);
            if (key is null)
            {
                commitRows.Add(row);
                continue;
            }

            if (!seen.Add(key.Value))
            {
                skippedRows += 1;
                continue;
            }

            commitRows.Add(row);
        }

        return new ImportCommitDeduplicationResult(commitRows, skippedRows);
    }

    private static async Task<HashSet<InvoiceKey>> LoadExistingInvoiceKeysAsync(
        ConGNoDbContext db,
        IReadOnlyList<InvoiceKey> keys,
        CancellationToken ct)
    {
        var sellerCodes = keys.Select(k => k.SellerTaxCode).Distinct().ToList();
        var customerCodes = keys.Select(k => k.CustomerTaxCode).Distinct().ToList();
        var invoiceNos = keys.Select(k => k.InvoiceNo).Distinct().ToList();
        var issueDates = keys.Select(k => k.IssueDate).Distinct().ToList();
        var seriesList = keys.Select(k => k.InvoiceSeries).Distinct().ToList();

        var existing = await db.Invoices
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

    private static async Task<HashSet<AdvanceKey>> LoadExistingAdvanceKeysAsync(
        ConGNoDbContext db,
        IReadOnlyList<AdvanceKey> keys,
        CancellationToken ct)
    {
        var sellerCodes = keys.Select(k => k.SellerTaxCode).Distinct().ToList();
        var customerCodes = keys.Select(k => k.CustomerTaxCode).Distinct().ToList();
        var advanceNos = keys.Select(k => k.AdvanceNo).Distinct().ToList();

        var existing = await db.Advances
            .AsNoTracking()
            .Where(a => a.DeletedAt == null)
            .Where(a => a.AdvanceNo != null)
            .Where(a =>
                sellerCodes.Contains(a.SellerTaxCode) &&
                customerCodes.Contains(a.CustomerTaxCode) &&
                advanceNos.Contains(a.AdvanceNo!))
            .Select(a => new
            {
                a.SellerTaxCode,
                a.CustomerTaxCode,
                a.AdvanceNo
            })
            .ToListAsync(ct);

        return new HashSet<AdvanceKey>(existing.Select(a => new AdvanceKey(
            NormalizeKeyPart(a.SellerTaxCode),
            NormalizeKeyPart(a.CustomerTaxCode),
            NormalizeKeyPart(a.AdvanceNo!))));
    }

    private static async Task<HashSet<ReceiptKey>> LoadExistingReceiptKeysAsync(
        ConGNoDbContext db,
        IReadOnlyList<ReceiptKey> keys,
        CancellationToken ct)
    {
        var sellerCodes = keys.Select(k => k.SellerTaxCode).Distinct().ToList();
        var customerCodes = keys.Select(k => k.CustomerTaxCode).Distinct().ToList();
        var receiptNos = keys.Select(k => k.ReceiptNo).Distinct().ToList();

        var existing = await db.Receipts
            .AsNoTracking()
            .Where(r => r.DeletedAt == null)
            .Where(r => r.ReceiptNo != null)
            .Where(r =>
                sellerCodes.Contains(r.SellerTaxCode) &&
                customerCodes.Contains(r.CustomerTaxCode) &&
                receiptNos.Contains(r.ReceiptNo!))
            .Select(r => new
            {
                r.SellerTaxCode,
                r.CustomerTaxCode,
                r.ReceiptNo
            })
            .ToListAsync(ct);

        return new HashSet<ReceiptKey>(existing.Select(r => new ReceiptKey(
            NormalizeKeyPart(r.SellerTaxCode),
            NormalizeKeyPart(r.CustomerTaxCode),
            NormalizeKeyPart(r.ReceiptNo!))));
    }

    private static InvoiceKey? TryBuildInvoiceKey(string rawData)
    {
        using var doc = JsonDocument.Parse(rawData);
        return TryBuildInvoiceKey(doc.RootElement);
    }

    private static AdvanceKey? TryBuildAdvanceKey(string rawData)
    {
        using var doc = JsonDocument.Parse(rawData);
        return TryBuildAdvanceKey(doc.RootElement);
    }

    private static ReceiptKey? TryBuildReceiptKey(string rawData)
    {
        using var doc = JsonDocument.Parse(rawData);
        return TryBuildReceiptKey(doc.RootElement);
    }

    private static InvoiceKey? TryBuildInvoiceKey(JsonElement raw)
    {
        var issueDate = ImportCommitJson.GetDate(raw, "issue_date");
        if (issueDate is null)
        {
            return null;
        }

        var seller = NormalizeKeyPart(ImportCommitJson.GetString(raw, "seller_tax_code"));
        var customer = NormalizeKeyPart(ImportCommitJson.ResolveInvoiceCustomerTaxCode(raw));
        var invoiceNo = NormalizeKeyPart(ImportCommitJson.GetString(raw, "invoice_no"));

        if (string.IsNullOrWhiteSpace(seller) ||
            string.IsNullOrWhiteSpace(customer) ||
            string.IsNullOrWhiteSpace(invoiceNo))
        {
            return null;
        }

        return new InvoiceKey(
            seller,
            customer,
            NormalizeKeyPart(ImportCommitJson.GetString(raw, "invoice_series")),
            invoiceNo,
            issueDate.Value);
    }

    private static AdvanceKey? TryBuildAdvanceKey(JsonElement raw)
    {
        var seller = NormalizeKeyPart(ImportCommitJson.GetString(raw, "seller_tax_code"));
        var customer = NormalizeKeyPart(ImportCommitJson.GetString(raw, "customer_tax_code"));
        var advanceNo = NormalizeKeyPart(ImportCommitJson.GetString(raw, "advance_no"));

        if (string.IsNullOrWhiteSpace(seller) ||
            string.IsNullOrWhiteSpace(customer) ||
            string.IsNullOrWhiteSpace(advanceNo))
        {
            return null;
        }

        return new AdvanceKey(seller, customer, advanceNo);
    }

    private static ReceiptKey? TryBuildReceiptKey(JsonElement raw)
    {
        var seller = NormalizeKeyPart(ImportCommitJson.GetString(raw, "seller_tax_code"));
        var customer = NormalizeKeyPart(ImportCommitJson.GetString(raw, "customer_tax_code"));
        var receiptNo = NormalizeKeyPart(ImportCommitJson.GetString(raw, "receipt_no"));

        if (string.IsNullOrWhiteSpace(seller) ||
            string.IsNullOrWhiteSpace(customer) ||
            string.IsNullOrWhiteSpace(receiptNo))
        {
            return null;
        }

        return new ReceiptKey(seller, customer, receiptNo);
    }

    private static List<string> ParseMessages(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string NormalizeKeyPart(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private readonly record struct InvoiceKey(
        string SellerTaxCode,
        string CustomerTaxCode,
        string InvoiceSeries,
        string InvoiceNo,
        DateOnly IssueDate);

    private readonly record struct AdvanceKey(
        string SellerTaxCode,
        string CustomerTaxCode,
        string AdvanceNo);

    private readonly record struct ReceiptKey(
        string SellerTaxCode,
        string CustomerTaxCode,
        string ReceiptNo);
}

internal readonly record struct ImportCommitDeduplicationResult(
    IReadOnlyList<ImportStagingRow> Rows,
    int SkippedRows);
