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
