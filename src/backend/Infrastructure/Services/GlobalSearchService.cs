using CongNoGolden.Application.Search;
using CongNoGolden.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed class GlobalSearchService : IGlobalSearchService
{
    private readonly ConGNoDbContext _db;

    public GlobalSearchService(ConGNoDbContext db)
    {
        _db = db;
    }

    public async Task<GlobalSearchResultDto> SearchAsync(string query, int top, CancellationToken ct)
    {
        var term = query.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            return new GlobalSearchResultDto(query, 0, [], [], []);
        }

        var normalized = term.ToLowerInvariant();
        var candidateLimit = Math.Clamp(top * 5, 20, 120);

        var customers = await SearchCustomersAsync(term, normalized, top, candidateLimit, ct);
        var invoices = await SearchInvoicesAsync(term, normalized, top, candidateLimit, ct);
        var receipts = await SearchReceiptsAsync(term, normalized, top, candidateLimit, ct);

        return new GlobalSearchResultDto(
            term,
            customers.Count + invoices.Count + receipts.Count,
            customers,
            invoices,
            receipts);
    }

    private async Task<IReadOnlyList<GlobalSearchCustomerItem>> SearchCustomersAsync(
        string term,
        string normalized,
        int top,
        int candidateLimit,
        CancellationToken ct)
    {
        var candidates = await _db.Customers
            .AsNoTracking()
            .Where(c =>
                c.TaxCode.ToLower().Contains(normalized) ||
                (EF.Property<string>(c, "NameSearch") ?? string.Empty).Contains(normalized))
            .Select(c => new
            {
                c.TaxCode,
                c.Name
            })
            .Take(candidateLimit)
            .ToListAsync(ct);

        return candidates
            .OrderBy(c => ComputeCustomerRank(term, c.TaxCode, c.Name))
            .ThenBy(c => c.Name)
            .ThenBy(c => c.TaxCode)
            .Take(top)
            .Select(c => new GlobalSearchCustomerItem(c.TaxCode, c.Name))
            .ToList();
    }

    private async Task<IReadOnlyList<GlobalSearchInvoiceItem>> SearchInvoicesAsync(
        string term,
        string normalized,
        int top,
        int candidateLimit,
        CancellationToken ct)
    {
        var candidates = await (
            from invoice in _db.Invoices.AsNoTracking()
            join customer in _db.Customers.AsNoTracking()
                on invoice.CustomerTaxCode equals customer.TaxCode into customerJoin
            from customer in customerJoin.DefaultIfEmpty()
            where invoice.DeletedAt == null &&
                  (
                      invoice.InvoiceNo.ToLower().Contains(normalized) ||
                      invoice.CustomerTaxCode.ToLower().Contains(normalized) ||
                      (customer != null && customer.Name.ToLower().Contains(normalized))
                  )
            select new
            {
                invoice.Id,
                invoice.InvoiceNo,
                invoice.CustomerTaxCode,
                CustomerName = customer != null ? customer.Name : invoice.CustomerTaxCode,
                invoice.IssueDate,
                invoice.OutstandingAmount,
                invoice.Status
            })
            .Take(candidateLimit)
            .ToListAsync(ct);

        return candidates
            .OrderBy(i => ComputeInvoiceRank(term, i.InvoiceNo, i.CustomerTaxCode, i.CustomerName))
            .ThenByDescending(i => i.IssueDate)
            .ThenBy(i => i.InvoiceNo)
            .Take(top)
            .Select(i => new GlobalSearchInvoiceItem(
                i.Id,
                i.InvoiceNo,
                i.CustomerTaxCode,
                i.CustomerName,
                i.IssueDate,
                i.OutstandingAmount,
                i.Status))
            .ToList();
    }

    private async Task<IReadOnlyList<GlobalSearchReceiptItem>> SearchReceiptsAsync(
        string term,
        string normalized,
        int top,
        int candidateLimit,
        CancellationToken ct)
    {
        var candidates = await (
            from receipt in _db.Receipts.AsNoTracking()
            join customer in _db.Customers.AsNoTracking()
                on receipt.CustomerTaxCode equals customer.TaxCode into customerJoin
            from customer in customerJoin.DefaultIfEmpty()
            where receipt.DeletedAt == null &&
                  (
                      (receipt.ReceiptNo ?? string.Empty).ToLower().Contains(normalized) ||
                      receipt.CustomerTaxCode.ToLower().Contains(normalized) ||
                      (customer != null && customer.Name.ToLower().Contains(normalized))
                  )
            select new
            {
                receipt.Id,
                receipt.ReceiptNo,
                receipt.CustomerTaxCode,
                CustomerName = customer != null ? customer.Name : receipt.CustomerTaxCode,
                receipt.ReceiptDate,
                receipt.Amount,
                receipt.Status
            })
            .Take(candidateLimit)
            .ToListAsync(ct);

        return candidates
            .OrderBy(r => ComputeReceiptRank(term, r.ReceiptNo, r.CustomerTaxCode, r.CustomerName))
            .ThenByDescending(r => r.ReceiptDate)
            .ThenBy(r => r.ReceiptNo ?? string.Empty)
            .Take(top)
            .Select(r => new GlobalSearchReceiptItem(
                r.Id,
                r.ReceiptNo,
                r.CustomerTaxCode,
                r.CustomerName,
                r.ReceiptDate,
                r.Amount,
                r.Status))
            .ToList();
    }

    internal static int ComputeCustomerRank(string term, string taxCode, string name)
    {
        return ComputeRank(term, taxCode, name);
    }

    internal static int ComputeInvoiceRank(string term, string invoiceNo, string customerTaxCode, string customerName)
    {
        return ComputeRank(term, invoiceNo, customerTaxCode, customerName);
    }

    internal static int ComputeReceiptRank(string term, string? receiptNo, string customerTaxCode, string customerName)
    {
        return ComputeRank(term, receiptNo ?? string.Empty, customerTaxCode, customerName);
    }

    private static int ComputeRank(string term, params string[] values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index] ?? string.Empty;
            if (value.Length == 0)
            {
                continue;
            }

            if (value.Equals(term, StringComparison.OrdinalIgnoreCase))
            {
                return index * 3;
            }

            if (value.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            {
                return index * 3 + 1;
            }

            if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return index * 3 + 2;
            }
        }

        return int.MaxValue;
    }
}
