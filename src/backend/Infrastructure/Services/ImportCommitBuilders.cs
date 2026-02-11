using System.Text.Json;
using CongNoGolden.Infrastructure.Data.Entities;

namespace CongNoGolden.Infrastructure.Services;

public static class ImportCommitBuilders
{
    public static Invoice BuildInvoice(JsonElement raw, Guid batchId, HashSet<string> sellerSet)
    {
        var seller = ImportCommitJson.GetString(raw, "seller_tax_code");
        EnsureSeller(seller, sellerSet);

        var issueDate = ImportCommitJson.GetDate(raw, "issue_date") ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var revenue = ImportCommitJson.GetDecimal(raw, "revenue_excl_vat");
        var vat = ImportCommitJson.GetDecimal(raw, "vat_amount");
        var total = ImportCommitJson.GetDecimal(raw, "total_amount");
        if (total <= 0)
        {
            total = revenue + vat;
        }

        return new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller,
            CustomerTaxCode = ImportCommitJson.GetString(raw, "customer_tax_code"),
            InvoiceTemplateCode = ImportCommitJson.GetString(raw, "invoice_template_code"),
            InvoiceSeries = ImportCommitJson.GetString(raw, "invoice_series"),
            InvoiceNo = ImportCommitJson.GetString(raw, "invoice_no"),
            IssueDate = issueDate,
            RevenueExclVat = revenue,
            VatAmount = vat,
            TotalAmount = total,
            OutstandingAmount = total,
            Note = ImportCommitJson.GetString(raw, "note"),
            InvoiceType = "NORMAL",
            Status = "OPEN",
            SourceBatchId = batchId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };
    }

    public static Advance BuildAdvance(JsonElement raw, Guid batchId, HashSet<string> sellerSet)
    {
        var seller = ImportCommitJson.GetString(raw, "seller_tax_code");
        EnsureSeller(seller, sellerSet);

        var date = ImportCommitJson.GetDate(raw, "advance_date") ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var amount = ImportCommitJson.GetDecimal(raw, "amount");

        return new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller,
            CustomerTaxCode = ImportCommitJson.GetString(raw, "customer_tax_code"),
            AdvanceNo = ImportCommitJson.GetString(raw, "advance_no"),
            AdvanceDate = date,
            Amount = amount,
            OutstandingAmount = amount,
            Description = ImportCommitJson.GetString(raw, "description"),
            Status = "APPROVED",
            ApprovedAt = DateTimeOffset.UtcNow,
            SourceBatchId = batchId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };
    }

    public static Receipt BuildReceipt(JsonElement raw, Guid batchId, HashSet<string> sellerSet)
    {
        var seller = ImportCommitJson.GetString(raw, "seller_tax_code");
        EnsureSeller(seller, sellerSet);

        var method = ImportCommitJson.GetString(raw, "method");
        if (string.IsNullOrWhiteSpace(method))
        {
            method = "BANK";
        }

        return new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller,
            CustomerTaxCode = ImportCommitJson.GetString(raw, "customer_tax_code"),
            ReceiptNo = ImportCommitJson.GetString(raw, "receipt_no"),
            ReceiptDate = ImportCommitJson.GetDate(raw, "receipt_date") ?? DateOnly.FromDateTime(DateTime.UtcNow),
            AppliedPeriodStart = ImportCommitJson.GetDate(raw, "applied_period_start"),
            Amount = ImportCommitJson.GetDecimal(raw, "amount"),
            Method = method,
            Description = ImportCommitJson.GetString(raw, "description"),
            AllocationMode = "FIFO",
            UnallocatedAmount = 0,
            Status = "DRAFT",
            SourceBatchId = batchId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };
    }

    private static void EnsureSeller(string seller, HashSet<string> sellerSet)
    {
        if (!sellerSet.Contains(seller))
        {
            throw new InvalidOperationException("Seller not found.");
        }
    }
}
