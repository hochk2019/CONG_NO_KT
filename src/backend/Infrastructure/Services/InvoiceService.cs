using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Common.StatusCodes;
using CongNoGolden.Application.Invoices;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed class InvoiceService : IInvoiceService
{
    private readonly ConGNoDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public InvoiceService(ConGNoDbContext db, ICurrentUser currentUser, IAuditService auditService)
    {
        _db = db;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<PagedResult<InvoiceListItemDto>> ListAsync(InvoiceListRequest request, CancellationToken ct)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);

        var query = _db.Invoices.AsNoTracking()
            .Where(i => i.DeletedAt == null);

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
                return new PagedResult<InvoiceListItemDto>([], page, pageSize, 0);
            }
            else
            {
                var invoiceIds = await _db.ReceiptAllocations
                    .AsNoTracking()
                    .Where(a => a.InvoiceId != null)
                    .Join(
                        _db.Receipts.AsNoTracking().Where(r => r.DeletedAt == null),
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
                        return new PagedResult<InvoiceListItemDto>([], page, pageSize, 0);
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
                .Join(
                    _db.Receipts.AsNoTracking().Where(r => r.DeletedAt == null),
                    allocation => allocation.ReceiptId,
                    receipt => receipt.Id,
                    (allocation, receipt) => new { allocation.InvoiceId, receipt.ReceiptNo })
                .Where(r => r.ReceiptNo != null && EF.Functions.ILike(r.ReceiptNo, pattern))
                .Select(r => r.InvoiceId!.Value)
                .Distinct()
                .ToListAsync(ct);

            if (invoiceIds.Count == 0)
            {
                return new PagedResult<InvoiceListItemDto>([], page, pageSize, 0);
            }

            query = query.Where(i => invoiceIds.Contains(i.Id));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(
                _db.Customers.AsNoTracking(),
                invoice => invoice.CustomerTaxCode,
                customer => customer.TaxCode,
                (invoice, customer) => new { invoice, customer })
            .Select(row => new
            {
                row.invoice.Id,
                row.invoice.InvoiceNo,
                row.invoice.IssueDate,
                row.invoice.TotalAmount,
                row.invoice.OutstandingAmount,
                row.invoice.Status,
                row.invoice.Version,
                row.invoice.CustomerTaxCode,
                CustomerName = row.customer.Name,
                row.invoice.SellerTaxCode,
                SellerShortName = _db.Sellers
                    .Where(s => s.SellerTaxCode == row.invoice.SellerTaxCode)
                    .Select(s => s.ShortName)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var invoiceIdsPage = items.Select(i => i.Id).ToList();
        Dictionary<Guid, List<InvoiceReceiptRefDto>> receiptLookup;
        Dictionary<Guid, List<InvoiceRefDto>> reductionInvoiceLookup;
        Dictionary<Guid, List<InvoiceRefDto>> reducedInvoiceLookup;
        if (invoiceIdsPage.Count == 0)
        {
            receiptLookup = [];
            reductionInvoiceLookup = [];
            reducedInvoiceLookup = [];
        }
        else
        {
            var receiptRows = await _db.ReceiptAllocations
                .AsNoTracking()
                .Where(a => a.InvoiceId != null && invoiceIdsPage.Contains(a.InvoiceId.Value))
                .Join(
                    _db.Receipts.AsNoTracking().Where(r => r.DeletedAt == null),
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
                        .Select(r => new InvoiceReceiptRefDto(r.Id, r.ReceiptNo ?? string.Empty, r.ReceiptDate, r.Amount))
                        .ToList());

            var reductionRows = await _db.InvoiceReductionApplications
                .AsNoTracking()
                .Where(application => invoiceIdsPage.Contains(application.AppliedInvoiceId))
                .Join(
                    _db.Invoices.AsNoTracking().Where(invoice => invoice.DeletedAt == null),
                    application => application.ReductionInvoiceId,
                    invoice => invoice.Id,
                    (application, invoice) => new
                    {
                        application.AppliedInvoiceId,
                        invoice.Id,
                        invoice.InvoiceNo,
                        invoice.IssueDate,
                        application.Amount
                    })
                .ToListAsync(ct);

            reductionInvoiceLookup = reductionRows
                .GroupBy(row => row.AppliedInvoiceId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(row => row.IssueDate)
                        .ThenBy(row => row.InvoiceNo)
                        .Select(row => new InvoiceRefDto(
                            row.Id,
                            row.InvoiceNo,
                            row.IssueDate,
                            row.Amount))
                        .ToList());

            var reducedRows = await _db.InvoiceReductionApplications
                .AsNoTracking()
                .Where(application => invoiceIdsPage.Contains(application.ReductionInvoiceId))
                .Join(
                    _db.Invoices.AsNoTracking().Where(invoice => invoice.DeletedAt == null),
                    application => application.AppliedInvoiceId,
                    invoice => invoice.Id,
                    (application, invoice) => new
                    {
                        application.ReductionInvoiceId,
                        invoice.Id,
                        invoice.InvoiceNo,
                        invoice.IssueDate,
                        application.Amount
                    })
                .ToListAsync(ct);

            reducedInvoiceLookup = reducedRows
                .GroupBy(row => row.ReductionInvoiceId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(row => row.IssueDate)
                        .ThenBy(row => row.InvoiceNo)
                        .Select(row => new InvoiceRefDto(
                            row.Id,
                            row.InvoiceNo,
                            row.IssueDate,
                            row.Amount))
                        .ToList());
        }

        var mapped = items
            .Select(i => new InvoiceListItemDto(
                i.Id,
                i.InvoiceNo,
                i.IssueDate,
                i.TotalAmount,
                i.OutstandingAmount,
                i.Status,
                i.Version,
                i.CustomerTaxCode,
                i.CustomerName,
                i.SellerTaxCode,
                i.SellerShortName,
                receiptLookup.TryGetValue(i.Id, out var receipts) ? receipts : [],
                reductionInvoiceLookup.TryGetValue(i.Id, out var reductionInvoices) ? reductionInvoices : [],
                reducedInvoiceLookup.TryGetValue(i.Id, out var reducedInvoices) ? reducedInvoices : []))
            .ToList();

        return new PagedResult<InvoiceListItemDto>(mapped, page, pageSize, total);
    }

    public async Task<InvoiceVoidResult> VoidAsync(Guid invoiceId, InvoiceVoidRequest request, CancellationToken ct)
    {
        EnsureCanManageInvoices();

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Void reason is required.");
        }

        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.DeletedAt == null, ct);
        if (invoice is null)
        {
            throw new InvalidOperationException("Invoice not found.");
        }

        if (request.Version is null)
        {
            throw new InvalidOperationException("Invoice version is required.");
        }

        if (request.Version.Value != invoice.Version)
        {
            throw new ConcurrencyException("Invoice was updated by another user. Please refresh.");
        }

        if (invoice.Status == "VOID")
        {
            throw new InvalidOperationException("Invoice already voided.");
        }

        var allocations = await _db.ReceiptAllocations
            .Where(a => a.InvoiceId == invoice.Id)
            .ToListAsync(ct);

        var hasAllocations = allocations.Count > 0;
        if (hasAllocations)
        {
            if (!request.Force)
            {
                throw new InvalidOperationException("Invoice has receipts and requires confirmation.");
            }
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.TaxCode == invoice.CustomerTaxCode, ct);
        var previousStatus = invoice.Status;
        var now = DateTimeOffset.UtcNow;
        var createdHeldCreditAmount = 0m;
        var createdHeldCreditCount = 0;
        var restoredHeldCreditAmount = 0m;
        var restoredHeldCreditCount = 0;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (hasAllocations)
        {
            var heldCreditGroups = allocations
                .Where(a => a.HeldCreditId.HasValue)
                .GroupBy(a => a.HeldCreditId!.Value)
                .Select(group => new
                {
                    HeldCreditId = group.Key,
                    Amount = group.Sum(item => item.Amount)
                })
                .ToList();

            if (heldCreditGroups.Count > 0)
            {
                var heldCreditIds = heldCreditGroups.Select(group => group.HeldCreditId).ToList();
                var heldCredits = await _db.ReceiptHeldCredits
                    .Where(item => heldCreditIds.Contains(item.Id))
                    .ToDictionaryAsync(item => item.Id, ct);

                foreach (var group in heldCreditGroups)
                {
                    if (!heldCredits.TryGetValue(group.HeldCreditId, out var heldCredit))
                    {
                        throw new InvalidOperationException("Held credit not found for invoice allocation.");
                    }

                    heldCredit.AmountRemaining += group.Amount;
                    heldCredit.Status = ComputeHeldCreditStatus(heldCredit);
                    heldCredit.UpdatedAt = now;
                    heldCredit.Version += 1;

                    restoredHeldCreditAmount += group.Amount;
                    restoredHeldCreditCount += 1;
                }
            }

            var genericAllocationGroups = allocations
                .Where(a => !a.HeldCreditId.HasValue)
                .GroupBy(a => a.ReceiptId)
                .Select(group => new
                {
                    ReceiptId = group.Key,
                    Amount = group.Sum(item => item.Amount)
                })
                .ToList();

            foreach (var group in genericAllocationGroups)
            {
                _db.ReceiptHeldCredits.Add(new ReceiptHeldCredit
                {
                    Id = Guid.NewGuid(),
                    ReceiptId = group.ReceiptId,
                    OriginalInvoiceId = invoice.Id,
                    OriginalAmount = group.Amount,
                    AmountRemaining = group.Amount,
                    Status = ReceiptHeldCreditStatusCodes.Holding,
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Version = 0
                });

                createdHeldCreditAmount += group.Amount;
                createdHeldCreditCount += 1;
            }

            _db.ReceiptAllocations.RemoveRange(allocations);
        }

        invoice.Status = "VOID";
        invoice.OutstandingAmount = 0;
        invoice.UpdatedAt = now;
        invoice.Version += 1;

        if (customer is not null)
        {
            customer.CurrentBalance -= invoice.TotalAmount;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _auditService.LogAsync(
            "INVOICE_VOID",
            "Invoice",
            invoice.Id.ToString(),
            new { status = previousStatus },
            new
            {
                status = invoice.Status,
                reason = request.Reason,
                heldCreditAmount = createdHeldCreditAmount,
                heldCreditCount = createdHeldCreditCount,
                restoredHeldCreditAmount,
                restoredHeldCreditCount
            },
            ct);

        return new InvoiceVoidResult(
            invoice.Id,
            invoice.Status,
            invoice.Version,
            invoice.OutstandingAmount,
            null,
            createdHeldCreditAmount,
            createdHeldCreditCount,
            restoredHeldCreditAmount,
            restoredHeldCreditCount);
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
            term = trimmed[3..].Trim();
        }
        else if (upper.StartsWith("PT:"))
        {
            mode = "RECEIPT";
            term = trimmed[3..].Trim();
        }
        else if (upper.StartsWith("TH:"))
        {
            mode = "ADVANCE";
            term = trimmed[3..].Trim();
        }

        if (string.IsNullOrWhiteSpace(term))
        {
            return (null, mode.Length == 0 ? null : mode);
        }

        return (term, mode.Length == 0 ? null : mode);
    }

    private void EnsureCanManageInvoices()
    {
        var roles = new HashSet<string>(_currentUser.Roles, StringComparer.OrdinalIgnoreCase);
        if (roles.Contains("Admin") || roles.Contains("Supervisor"))
        {
            return;
        }

        throw new UnauthorizedAccessException("Not allowed to manage invoices.");
    }

    private static string ComputeHeldCreditStatus(ReceiptHeldCredit heldCredit)
    {
        if (heldCredit.AmountRemaining <= 0)
        {
            return ReceiptHeldCreditStatusCodes.Reapplied;
        }

        if (heldCredit.AmountRemaining >= heldCredit.OriginalAmount)
        {
            return ReceiptHeldCreditStatusCodes.Holding;
        }

        return ReceiptHeldCreditStatusCodes.Partial;
    }
}
