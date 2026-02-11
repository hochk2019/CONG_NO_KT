using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Invoices;
using CongNoGolden.Infrastructure.Data;
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
        Guid? replacementId = null;
        if (hasAllocations)
        {
            if (!request.Force)
            {
                throw new InvalidOperationException("Invoice has receipts and requires confirmation.");
            }

            if (!request.ReplacementInvoiceId.HasValue)
            {
                throw new InvalidOperationException("Replacement invoice is required when receipts exist.");
            }

            replacementId = request.ReplacementInvoiceId.Value;
        }

        var replacement = replacementId.HasValue
            ? await _db.Invoices.FirstOrDefaultAsync(i => i.Id == replacementId.Value && i.DeletedAt == null, ct)
            : null;

        if (replacementId.HasValue)
        {
            if (replacement is null)
            {
                throw new InvalidOperationException("Replacement invoice not found.");
            }

            if (replacement.Status == "VOID")
            {
                throw new InvalidOperationException("Replacement invoice is voided.");
            }

            if (replacement.Id == invoice.Id)
            {
                throw new InvalidOperationException("Replacement invoice must be different from original.");
            }

            if (replacement.SellerTaxCode != invoice.SellerTaxCode ||
                replacement.CustomerTaxCode != invoice.CustomerTaxCode)
            {
                throw new InvalidOperationException("Replacement invoice must match seller and customer.");
            }
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.TaxCode == invoice.CustomerTaxCode, ct);
        var previousStatus = invoice.Status;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (hasAllocations && replacement is not null)
        {
            var movedAmount = allocations.Sum(a => a.Amount);
            var replacementAllocated = await _db.ReceiptAllocations
                .Where(a => a.InvoiceId == replacement.Id)
                .SumAsync(a => a.Amount, ct);

            var totalAllocated = replacementAllocated + movedAmount;
            if (totalAllocated > replacement.TotalAmount)
            {
                throw new InvalidOperationException("Replacement invoice allocation exceeds total amount.");
            }

            foreach (var allocation in allocations)
            {
                allocation.InvoiceId = replacement.Id;
            }

            replacement.OutstandingAmount = Math.Max(0, replacement.TotalAmount - totalAllocated);
            replacement.Status = replacement.OutstandingAmount == 0 ? "PAID" : "PARTIAL";
            replacement.UpdatedAt = DateTimeOffset.UtcNow;
            replacement.Version += 1;
        }

        invoice.Status = "VOID";
        invoice.OutstandingAmount = 0;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;
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
                replacementInvoiceId = replacementId
            },
            ct);

        return new InvoiceVoidResult(
            invoice.Id,
            invoice.Status,
            invoice.Version,
            invoice.OutstandingAmount,
            replacementId);
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
}
