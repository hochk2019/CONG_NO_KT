using CongNoGolden.Application.Common;
using CongNoGolden.Application.Customers;
using CongNoGolden.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed class CustomerService : ICustomerService
{
    private readonly ConGNoDbContext _db;

    public CustomerService(ConGNoDbContext db)
    {
        _db = db;
    }

    private static (string? Term, string? Mode) ParseSearch(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (null, null);
        }

        var trimmed = raw.Trim();
        var upper = trimmed.ToUpperInvariant();
        var mode = string.Empty;
        var term = trimmed;

        if (upper.StartsWith("HD:") || upper.StartsWith("HĐ:"))
        {
            mode = "INVOICE";
            term = trimmed.Substring(3).Trim();
        }
        else if (upper.StartsWith("PT:"))
        {
            mode = "RECEIPT";
            term = trimmed.Substring(3).Trim();
        }
        else if (upper.StartsWith("TH:"))
        {
            mode = "ADVANCE";
            term = trimmed.Substring(3).Trim();
        }

        if (string.IsNullOrWhiteSpace(term))
        {
            return (null, mode.Length == 0 ? null : mode);
        }

        return (term, mode.Length == 0 ? null : mode);
    }

    public async Task<PagedResult<CustomerListItem>> ListAsync(CustomerListRequest request, CancellationToken ct)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);

        var query = _db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToUpperInvariant();
            query = query.Where(c => c.Status == status);
        }

        if (request.OwnerId.HasValue)
        {
            query = query.Where(c => c.AccountantOwnerId == request.OwnerId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            var termPattern = $"%{term}%";
            var namePattern = $"%{term.ToLowerInvariant()}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.TaxCode, termPattern) ||
                EF.Functions.ILike(
                    EF.Property<string>(c, "NameSearch"),
                    NpgsqlFullTextSearchDbFunctionsExtensions.Unaccent(EF.Functions, namePattern)));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderBy(c => c.Name)
            .ThenBy(c => c.TaxCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.TaxCode,
                c.Name,
                c.AccountantOwnerId,
                c.CurrentBalance,
                c.Status
            })
            .ToListAsync(ct);

        var ownerIds = rows
            .Where(r => r.AccountantOwnerId.HasValue)
            .Select(r => r.AccountantOwnerId!.Value)
            .Distinct()
            .ToList();

        var ownerLookup = ownerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users
                .Where(u => ownerIds.Contains(u.Id))
                .ToDictionaryAsync(
                    u => u.Id,
                    u => string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName,
                    ct);

        var items = rows.Select(r => new CustomerListItem(
                r.TaxCode,
                r.Name,
                r.AccountantOwnerId.HasValue && ownerLookup.TryGetValue(r.AccountantOwnerId.Value, out var name)
                    ? name
                    : null,
                r.CurrentBalance,
                r.Status))
            .ToList();

        return new PagedResult<CustomerListItem>(items, page, pageSize, total);
    }

    public async Task<CustomerDetailDto?> GetAsync(string taxCode, CancellationToken ct)
    {
        var key = taxCode.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TaxCode == key, ct);

        if (customer is null)
        {
            return null;
        }

        var owner = customer.AccountantOwnerId.HasValue
            ? await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == customer.AccountantOwnerId.Value, ct)
            : null;

        var manager = customer.ManagerUserId.HasValue
            ? await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == customer.ManagerUserId.Value, ct)
            : null;

        var ownerName = owner is null
            ? null
            : string.IsNullOrWhiteSpace(owner.FullName) ? owner.Username : owner.FullName;

        var managerName = manager is null
            ? null
            : string.IsNullOrWhiteSpace(manager.FullName) ? manager.Username : manager.FullName;

        return new CustomerDetailDto(
            customer.TaxCode,
            customer.Name,
            customer.Address,
            customer.Email,
            customer.Phone,
            customer.Status,
            customer.CurrentBalance,
            customer.PaymentTermsDays,
            customer.CreditLimit,
            customer.AccountantOwnerId,
            ownerName,
            customer.ManagerUserId,
            managerName,
            customer.CreatedAt,
            customer.UpdatedAt);
    }

    public async Task<PagedResult<CustomerInvoiceDto>> ListInvoicesAsync(
        string taxCode,
        CustomerRelationRequest request,
        CancellationToken ct)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);

        var query = _db.Invoices.AsNoTracking()
            .Where(i => i.DeletedAt == null && i.CustomerTaxCode == taxCode);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToUpperInvariant();
            query = query.Where(i => i.Status == status);
        }

        var (searchTerm, searchMode) = ParseSearch(request.Search);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm}%";
            if (searchMode == "INVOICE")
            {
                query = query.Where(i => EF.Functions.ILike(i.InvoiceNo, pattern));
            }
            else if (searchMode == "ADVANCE")
            {
                return new PagedResult<CustomerInvoiceDto>(new List<CustomerInvoiceDto>(), page, pageSize, 0);
            }
            else
            {
                var invoiceIds = await _db.ReceiptAllocations
                    .AsNoTracking()
                    .Where(a => a.InvoiceId != null)
                    .Join(_db.Receipts.AsNoTracking().Where(r => r.DeletedAt == null),
                        allocation => allocation.ReceiptId,
                        receipt => receipt.Id,
                        (allocation, receipt) => new { allocation.InvoiceId, receipt.ReceiptNo })
                    .Where(r => r.ReceiptNo != null && EF.Functions.ILike(r.ReceiptNo, pattern))
                    .Select(r => r.InvoiceId!.Value)
                    .Distinct()
                    .ToListAsync(ct);

                if (searchMode == "RECEIPT")
                {
                    if (invoiceIds.Count == 0)
                    {
                        return new PagedResult<CustomerInvoiceDto>(new List<CustomerInvoiceDto>(), page, pageSize, 0);
                    }

                    query = query.Where(i => invoiceIds.Contains(i.Id));
                }
                else
                {
                    query = query.Where(i => EF.Functions.ILike(i.InvoiceNo, pattern) || invoiceIds.Contains(i.Id));
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.DocumentNo))
        {
            var term = request.DocumentNo.Trim();
            var pattern = $"%{term}%";
            query = query.Where(i => EF.Functions.ILike(i.InvoiceNo, pattern));
        }

        if (request.From.HasValue)
        {
            query = query.Where(i => i.IssueDate >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(i => i.IssueDate <= request.To.Value);
        }

        if (string.IsNullOrWhiteSpace(searchTerm) && !string.IsNullOrWhiteSpace(request.ReceiptNo))
        {
            var term = request.ReceiptNo.Trim();
            var pattern = $"%{term}%";
            var invoiceIds = await _db.ReceiptAllocations
                .AsNoTracking()
                .Where(a => a.InvoiceId != null)
                .Join(_db.Receipts.AsNoTracking().Where(r => r.DeletedAt == null),
                    allocation => allocation.ReceiptId,
                    receipt => receipt.Id,
                    (allocation, receipt) => new { allocation.InvoiceId, receipt.ReceiptNo })
                .Where(r => r.ReceiptNo != null && EF.Functions.ILike(r.ReceiptNo, pattern))
                .Select(r => r.InvoiceId!.Value)
                .Distinct()
                .ToListAsync(ct);

            if (invoiceIds.Count == 0)
            {
                return new PagedResult<CustomerInvoiceDto>(new List<CustomerInvoiceDto>(), page, pageSize, 0);
            }

            query = query.Where(i => invoiceIds.Contains(i.Id));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNo,
                i.IssueDate,
                i.TotalAmount,
                i.OutstandingAmount,
                i.Status,
                i.Version,
                i.SellerTaxCode,
                SellerShortName = _db.Sellers
                    .Where(s => s.SellerTaxCode == i.SellerTaxCode)
                    .Select(s => s.ShortName)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var invoiceIdsPage = items.Select(i => i.Id).ToList();
        Dictionary<Guid, List<CustomerReceiptRefDto>> receiptLookup;
        if (invoiceIdsPage.Count == 0)
        {
            receiptLookup = new Dictionary<Guid, List<CustomerReceiptRefDto>>();
        }
        else
        {
            var receiptRows = await _db.ReceiptAllocations
                .AsNoTracking()
                .Where(a => a.InvoiceId != null && invoiceIdsPage.Contains(a.InvoiceId!.Value))
                .Join(_db.Receipts.AsNoTracking().Where(r => r.DeletedAt == null),
                    allocation => allocation.ReceiptId,
                    receipt => receipt.Id,
                    (allocation, receipt) => new
                    {
                        InvoiceId = allocation.InvoiceId!.Value,
                        receipt.Id,
                        receipt.ReceiptNo,
                        receipt.ReceiptDate,
                        allocation.Amount
                    })
                .ToListAsync(ct);

            receiptLookup = receiptRows
                .GroupBy(r => r.InvoiceId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .Select(r => new CustomerReceiptRefDto(r.Id, r.ReceiptNo, r.ReceiptDate, r.Amount))
                        .ToList());
        }

        var mapped = items.Select(i => new CustomerInvoiceDto(
                i.Id,
                i.InvoiceNo,
                i.IssueDate,
                i.TotalAmount,
                i.OutstandingAmount,
                i.Status,
                i.Version,
                i.SellerTaxCode,
                i.SellerShortName,
                receiptLookup.TryGetValue(i.Id, out var receipts) ? receipts : Array.Empty<CustomerReceiptRefDto>()))
            .ToList();

        return new PagedResult<CustomerInvoiceDto>(mapped, page, pageSize, total);
    }

    public async Task<PagedResult<CustomerAdvanceDto>> ListAdvancesAsync(
        string taxCode,
        CustomerRelationRequest request,
        CancellationToken ct)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);

        var query = _db.Advances.AsNoTracking()
            .Where(a => a.DeletedAt == null && a.CustomerTaxCode == taxCode);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToUpperInvariant();
            query = query.Where(a => a.Status == status);
        }

        var (searchTerm, searchMode) = ParseSearch(request.Search);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm}%";
            if (searchMode == "ADVANCE")
            {
                query = query.Where(a => a.AdvanceNo != null && EF.Functions.ILike(a.AdvanceNo, pattern));
            }
            else if (searchMode == "INVOICE")
            {
                return new PagedResult<CustomerAdvanceDto>(new List<CustomerAdvanceDto>(), page, pageSize, 0);
            }
            else
            {
                var advanceIds = await _db.ReceiptAllocations
                    .AsNoTracking()
                    .Where(a => a.AdvanceId != null)
                    .Join(_db.Receipts.AsNoTracking().Where(r => r.DeletedAt == null),
                        allocation => allocation.ReceiptId,
                        receipt => receipt.Id,
                        (allocation, receipt) => new { allocation.AdvanceId, receipt.ReceiptNo })
                    .Where(r => r.ReceiptNo != null && EF.Functions.ILike(r.ReceiptNo, pattern))
                    .Select(r => r.AdvanceId!.Value)
                    .Distinct()
                    .ToListAsync(ct);

                if (searchMode == "RECEIPT")
                {
                    if (advanceIds.Count == 0)
                    {
                        return new PagedResult<CustomerAdvanceDto>(new List<CustomerAdvanceDto>(), page, pageSize, 0);
                    }

                    query = query.Where(a => advanceIds.Contains(a.Id));
                }
                else
                {
                    query = query.Where(a =>
                        (a.AdvanceNo != null && EF.Functions.ILike(a.AdvanceNo, pattern)) ||
                        advanceIds.Contains(a.Id));
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.DocumentNo))
        {
            var term = request.DocumentNo.Trim();
            var pattern = $"%{term}%";
            query = query.Where(a => a.AdvanceNo != null && EF.Functions.ILike(a.AdvanceNo, pattern));
        }

        if (request.From.HasValue)
        {
            query = query.Where(a => a.AdvanceDate >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(a => a.AdvanceDate <= request.To.Value);
        }

        if (string.IsNullOrWhiteSpace(searchTerm) && !string.IsNullOrWhiteSpace(request.ReceiptNo))
        {
            var term = request.ReceiptNo.Trim();
            var pattern = $"%{term}%";
            var advanceIds = await _db.ReceiptAllocations
                .AsNoTracking()
                .Where(a => a.AdvanceId != null)
                .Join(_db.Receipts.AsNoTracking().Where(r => r.DeletedAt == null),
                    allocation => allocation.ReceiptId,
                    receipt => receipt.Id,
                    (allocation, receipt) => new { allocation.AdvanceId, receipt.ReceiptNo })
                .Where(r => r.ReceiptNo != null && EF.Functions.ILike(r.ReceiptNo, pattern))
                .Select(r => r.AdvanceId!.Value)
                .Distinct()
                .ToListAsync(ct);

            if (advanceIds.Count == 0)
            {
                return new PagedResult<CustomerAdvanceDto>(new List<CustomerAdvanceDto>(), page, pageSize, 0);
            }

            query = query.Where(a => advanceIds.Contains(a.Id));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.AdvanceDate)
            .ThenByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.AdvanceNo,
                a.AdvanceDate,
                a.Amount,
                a.OutstandingAmount,
                a.Status,
                a.Version,
                a.SellerTaxCode,
                SellerShortName = _db.Sellers
                    .Where(s => s.SellerTaxCode == a.SellerTaxCode)
                    .Select(s => s.ShortName)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var advanceIdsPage = items.Select(a => a.Id).ToList();
        Dictionary<Guid, List<CustomerReceiptRefDto>> receiptLookup;
        if (advanceIdsPage.Count == 0)
        {
            receiptLookup = new Dictionary<Guid, List<CustomerReceiptRefDto>>();
        }
        else
        {
            var receiptRows = await _db.ReceiptAllocations
                .AsNoTracking()
                .Where(a => a.AdvanceId != null && advanceIdsPage.Contains(a.AdvanceId!.Value))
                .Join(_db.Receipts.AsNoTracking().Where(r => r.DeletedAt == null),
                    allocation => allocation.ReceiptId,
                    receipt => receipt.Id,
                    (allocation, receipt) => new
                    {
                        AdvanceId = allocation.AdvanceId!.Value,
                        receipt.Id,
                        receipt.ReceiptNo,
                        receipt.ReceiptDate,
                        allocation.Amount
                    })
                .ToListAsync(ct);

            receiptLookup = receiptRows
                .GroupBy(r => r.AdvanceId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .Select(r => new CustomerReceiptRefDto(r.Id, r.ReceiptNo, r.ReceiptDate, r.Amount))
                        .ToList());
        }

        var mapped = items.Select(a => new CustomerAdvanceDto(
                a.Id,
                a.AdvanceNo,
                a.AdvanceDate,
                a.Amount,
                a.OutstandingAmount,
                a.Status,
                a.Version,
                a.SellerTaxCode,
                a.SellerShortName,
                receiptLookup.TryGetValue(a.Id, out var receipts) ? receipts : Array.Empty<CustomerReceiptRefDto>()))
            .ToList();

        return new PagedResult<CustomerAdvanceDto>(mapped, page, pageSize, total);
    }

    public async Task<PagedResult<CustomerReceiptDto>> ListReceiptsAsync(
        string taxCode,
        CustomerRelationRequest request,
        CancellationToken ct)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);

        var query = _db.Receipts.AsNoTracking()
            .Where(r => r.DeletedAt == null && r.CustomerTaxCode == taxCode);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToUpperInvariant();
            query = query.Where(r => r.Status == status);
        }

        var (searchTerm, searchMode) = ParseSearch(request.Search);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm}%";
            if (searchMode == "RECEIPT")
            {
                query = query.Where(r => r.ReceiptNo != null && EF.Functions.ILike(r.ReceiptNo, pattern));
            }
            else
            {
                var invoiceReceiptIds = await _db.ReceiptAllocations
                    .AsNoTracking()
                    .Where(a => a.InvoiceId != null)
                    .Join(_db.Invoices.AsNoTracking().Where(i => i.DeletedAt == null),
                        allocation => allocation.InvoiceId!.Value,
                        invoice => invoice.Id,
                        (allocation, invoice) => new { allocation.ReceiptId, invoice.InvoiceNo })
                    .Where(r => r.InvoiceNo != null && EF.Functions.ILike(r.InvoiceNo, pattern))
                    .Select(r => r.ReceiptId)
                    .Distinct()
                    .ToListAsync(ct);

                var advanceReceiptIds = await _db.ReceiptAllocations
                    .AsNoTracking()
                    .Where(a => a.AdvanceId != null)
                    .Join(_db.Advances.AsNoTracking().Where(a => a.DeletedAt == null),
                        allocation => allocation.AdvanceId!.Value,
                        advance => advance.Id,
                        (allocation, advance) => new { allocation.ReceiptId, advance.AdvanceNo })
                    .Where(r => r.AdvanceNo != null && EF.Functions.ILike(r.AdvanceNo, pattern))
                    .Select(r => r.ReceiptId)
                    .Distinct()
                    .ToListAsync(ct);

                if (searchMode == "INVOICE")
                {
                    if (invoiceReceiptIds.Count == 0)
                    {
                        return new PagedResult<CustomerReceiptDto>(new List<CustomerReceiptDto>(), page, pageSize, 0);
                    }

                    query = query.Where(r => invoiceReceiptIds.Contains(r.Id));
                }
                else if (searchMode == "ADVANCE")
                {
                    if (advanceReceiptIds.Count == 0)
                    {
                        return new PagedResult<CustomerReceiptDto>(new List<CustomerReceiptDto>(), page, pageSize, 0);
                    }

                    query = query.Where(r => advanceReceiptIds.Contains(r.Id));
                }
                else
                {
                    query = query.Where(r =>
                        (r.ReceiptNo != null && EF.Functions.ILike(r.ReceiptNo, pattern)) ||
                        invoiceReceiptIds.Contains(r.Id) ||
                        advanceReceiptIds.Contains(r.Id));
                }
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.DocumentNo))
            {
                var term = request.DocumentNo.Trim();
                var pattern = $"%{term}%";
                query = query.Where(r => r.ReceiptNo != null && EF.Functions.ILike(r.ReceiptNo, pattern));
            }

            if (!string.IsNullOrWhiteSpace(request.ReceiptNo))
            {
                var term = request.ReceiptNo.Trim();
                var pattern = $"%{term}%";
                query = query.Where(r => r.ReceiptNo != null && EF.Functions.ILike(r.ReceiptNo, pattern));
            }
        }

        if (request.From.HasValue)
        {
            query = query.Where(r => r.ReceiptDate >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(r => r.ReceiptDate <= request.To.Value);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.ReceiptDate)
            .ThenByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new CustomerReceiptDto(
                r.Id,
                r.ReceiptNo,
                r.ReceiptDate,
                r.AppliedPeriodStart,
                r.Amount,
                r.UnallocatedAmount,
                r.Status,
                r.SellerTaxCode,
                _db.Sellers
                    .Where(s => s.SellerTaxCode == r.SellerTaxCode)
                    .Select(s => s.ShortName)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        return new PagedResult<CustomerReceiptDto>(items, page, pageSize, total);
    }
}
