using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Common.StatusCodes;
using CongNoGolden.Application.ReceiptHeldCredits;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed class ReceiptHeldCreditService : IReceiptHeldCreditService
{
    private readonly ConGNoDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public ReceiptHeldCreditService(
        ConGNoDbContext db,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _db = db;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<PagedResult<ReceiptHeldCreditListItem>> ListByCustomerAsync(
        string customerTaxCode,
        ReceiptHeldCreditListRequest request,
        CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var taxCode = customerTaxCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(taxCode))
        {
            throw new InvalidOperationException("Customer tax code is required.");
        }

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);

        var query =
            from heldCredit in _db.ReceiptHeldCredits.AsNoTracking()
            join receipt in _db.Receipts.AsNoTracking().Where(r => r.DeletedAt == null)
                on heldCredit.ReceiptId equals receipt.Id
            join invoice in _db.Invoices.AsNoTracking().Where(i => i.DeletedAt == null)
                on heldCredit.OriginalInvoiceId equals invoice.Id into invoiceJoin
            from invoice in invoiceJoin.DefaultIfEmpty()
            where receipt.CustomerTaxCode == taxCode
            select new
            {
                heldCredit.Id,
                heldCredit.Version,
                heldCredit.Status,
                ReceiptId = receipt.Id,
                receipt.ReceiptNo,
                receipt.ReceiptDate,
                heldCredit.OriginalInvoiceId,
                OriginalInvoiceNo = invoice != null ? invoice.InvoiceNo : null,
                OriginalInvoiceDate = invoice != null ? (DateOnly?)invoice.IssueDate : null,
                heldCredit.OriginalAmount,
                heldCredit.AmountRemaining,
                AppliedAmount = heldCredit.OriginalAmount - heldCredit.AmountRemaining,
                heldCredit.CreatedAt,
                heldCredit.UpdatedAt
            };

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToUpperInvariant();
            query = query.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(item =>
                (item.ReceiptNo != null && EF.Functions.ILike(item.ReceiptNo, pattern)) ||
                (item.OriginalInvoiceNo != null && EF.Functions.ILike(item.OriginalInvoiceNo, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(request.DocumentNo))
        {
            var pattern = $"%{request.DocumentNo.Trim()}%";
            query = query.Where(item => item.OriginalInvoiceNo != null && EF.Functions.ILike(item.OriginalInvoiceNo, pattern));
        }

        if (!string.IsNullOrWhiteSpace(request.ReceiptNo))
        {
            var pattern = $"%{request.ReceiptNo.Trim()}%";
            query = query.Where(item => item.ReceiptNo != null && EF.Functions.ILike(item.ReceiptNo, pattern));
        }

        if (request.From.HasValue)
        {
            var fromAt = new DateTimeOffset(request.From.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(item => item.CreatedAt >= fromAt);
        }

        if (request.To.HasValue)
        {
            var toExclusive = new DateTimeOffset(request.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(item => item.CreatedAt < toExclusive);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.ReceiptDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new ReceiptHeldCreditListItem(
                item.Id,
                item.Version,
                item.Status,
                item.ReceiptId,
                item.ReceiptNo,
                item.ReceiptDate,
                item.OriginalInvoiceId,
                item.OriginalInvoiceNo,
                item.OriginalInvoiceDate,
                item.OriginalAmount,
                item.AmountRemaining,
                item.AppliedAmount,
                item.CreatedAt,
                item.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResult<ReceiptHeldCreditListItem>(items, page, pageSize, total);
    }

    public async Task<ReceiptHeldCreditApplyResult> ApplyToInvoiceAsync(
        Guid heldCreditId,
        ReceiptHeldCreditApplyRequest request,
        CancellationToken ct)
    {
        if (request.Version is null)
        {
            throw new InvalidOperationException("Held credit version is required.");
        }

        var heldCredit = await _db.ReceiptHeldCredits
            .FirstOrDefaultAsync(item => item.Id == heldCreditId, ct);
        if (heldCredit is null)
        {
            throw new InvalidOperationException("Held credit not found.");
        }

        if (heldCredit.Version != request.Version.Value)
        {
            throw new ConcurrencyException("Held credit was updated by another user. Please refresh.");
        }

        if (heldCredit.AmountRemaining <= 0)
        {
            throw new InvalidOperationException("Held credit has no remaining amount.");
        }

        var sourceReceipt = await _db.Receipts
            .FirstOrDefaultAsync(r => r.Id == heldCredit.ReceiptId && r.DeletedAt == null, ct);
        if (sourceReceipt is null)
        {
            throw new InvalidOperationException("Source receipt not found.");
        }

        if (sourceReceipt.Status != ReceiptStatusCodes.Approved)
        {
            throw new InvalidOperationException("Source receipt is not approved.");
        }

        await EnsureCanManageCustomer(sourceReceipt.CustomerTaxCode, ct);

        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId && i.DeletedAt == null, ct);
        if (invoice is null)
        {
            throw new InvalidOperationException("Replacement invoice not found.");
        }

        if (invoice.Status == "VOID")
        {
            throw new InvalidOperationException("Replacement invoice is voided.");
        }

        if (invoice.SellerTaxCode != sourceReceipt.SellerTaxCode ||
            invoice.CustomerTaxCode != sourceReceipt.CustomerTaxCode)
        {
            throw new InvalidOperationException("Replacement invoice must match seller and customer.");
        }

        if (invoice.OutstandingAmount <= 0)
        {
            throw new InvalidOperationException("Replacement invoice has no outstanding amount.");
        }

        var now = DateTimeOffset.UtcNow;
        var appliedHeldAmount = 0m;
        var appliedGeneralCreditAmount = 0m;
        var previousHeldAmountRemaining = heldCredit.AmountRemaining;
        var previousHeldStatus = heldCredit.Status;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        appliedHeldAmount = Math.Min(heldCredit.AmountRemaining, invoice.OutstandingAmount);
        if (appliedHeldAmount > 0)
        {
            _db.ReceiptAllocations.Add(new ReceiptAllocation
            {
                Id = Guid.NewGuid(),
                ReceiptId = sourceReceipt.Id,
                HeldCreditId = heldCredit.Id,
                TargetType = "INVOICE",
                InvoiceId = invoice.Id,
                Amount = appliedHeldAmount,
                CreatedAt = now
            });

            heldCredit.AmountRemaining -= appliedHeldAmount;
            invoice.OutstandingAmount -= appliedHeldAmount;
            sourceReceipt.UpdatedAt = now;
            sourceReceipt.Version += 1;
        }

        if (request.UseGeneralCreditTopUp && invoice.OutstandingAmount > 0)
        {
            var receipts = await _db.Receipts
                .Where(r => r.DeletedAt == null && r.Status == ReceiptStatusCodes.Approved)
                .Where(r => r.SellerTaxCode == invoice.SellerTaxCode && r.CustomerTaxCode == invoice.CustomerTaxCode)
                .Where(r => r.UnallocatedAmount > 0)
                .Where(r => r.AutoAllocateEnabled)
                .OrderBy(r => r.ReceiptDate)
                .ThenBy(r => r.CreatedAt)
                .ToListAsync(ct);

            foreach (var receipt in receipts)
            {
                if (invoice.OutstandingAmount <= 0)
                {
                    break;
                }

                var allocated = Math.Min(invoice.OutstandingAmount, receipt.UnallocatedAmount);
                if (allocated <= 0)
                {
                    continue;
                }

                receipt.UnallocatedAmount -= allocated;
                receipt.AllocationStatus = receipt.UnallocatedAmount == 0
                    ? ReceiptAllocationStatusCodes.Allocated
                    : ReceiptAllocationStatusCodes.Partial;
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

                invoice.OutstandingAmount -= allocated;
                appliedGeneralCreditAmount += allocated;
            }
        }

        if (appliedHeldAmount <= 0 && appliedGeneralCreditAmount <= 0)
        {
            throw new InvalidOperationException("No amount could be applied to the replacement invoice.");
        }

        heldCredit.Status = ComputeHeldCreditStatus(heldCredit);
        heldCredit.UpdatedAt = now;
        heldCredit.Version += 1;

        invoice.Status = invoice.OutstandingAmount == 0 ? "PAID" : "PARTIAL";
        invoice.UpdatedAt = now;
        invoice.Version += 1;

        sourceReceipt.AllocationStatus = sourceReceipt.UnallocatedAmount > 0
            ? ReceiptAllocationStatusCodes.Partial
            : ReceiptAllocationStatusCodes.Allocated;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _auditService.LogAsync(
            "RECEIPT_HELD_CREDIT_APPLY",
            "ReceiptHeldCredit",
            heldCredit.Id.ToString(),
            new
            {
                amountRemaining = previousHeldAmountRemaining,
                status = previousHeldStatus
            },
            new
            {
                invoiceId = invoice.Id,
                appliedHeldAmount,
                appliedGeneralCreditAmount,
                amountRemaining = heldCredit.AmountRemaining,
                status = heldCredit.Status
            },
            ct);

        return new ReceiptHeldCreditApplyResult(
            heldCredit.Id,
            heldCredit.Version,
            heldCredit.Status,
            invoice.Id,
            appliedHeldAmount,
            appliedGeneralCreditAmount,
            heldCredit.AmountRemaining,
            invoice.OutstandingAmount);
    }

    public async Task<ReceiptHeldCreditReleaseResult> ReleaseToGeneralCreditAsync(
        Guid heldCreditId,
        ReceiptHeldCreditReleaseRequest request,
        CancellationToken ct)
    {
        if (request.Version is null)
        {
            throw new InvalidOperationException("Held credit version is required.");
        }

        var heldCredit = await _db.ReceiptHeldCredits
            .FirstOrDefaultAsync(item => item.Id == heldCreditId, ct);
        if (heldCredit is null)
        {
            throw new InvalidOperationException("Held credit not found.");
        }

        if (heldCredit.Version != request.Version.Value)
        {
            throw new ConcurrencyException("Held credit was updated by another user. Please refresh.");
        }

        if (heldCredit.AmountRemaining <= 0)
        {
            throw new InvalidOperationException("Held credit has no remaining amount to release.");
        }

        var receipt = await _db.Receipts
            .FirstOrDefaultAsync(r => r.Id == heldCredit.ReceiptId && r.DeletedAt == null, ct);
        if (receipt is null)
        {
            throw new InvalidOperationException("Source receipt not found.");
        }

        if (receipt.Status != ReceiptStatusCodes.Approved)
        {
            throw new InvalidOperationException("Source receipt is not approved.");
        }

        await EnsureCanManageCustomer(receipt.CustomerTaxCode, ct);

        var now = DateTimeOffset.UtcNow;
        var releasedAmount = heldCredit.AmountRemaining;
        var previousHeldStatus = heldCredit.Status;

        receipt.UnallocatedAmount += releasedAmount;
        receipt.UpdatedAt = now;
        receipt.Version += 1;
        receipt.AllocationStatus = await ResolveReceiptAllocationStatusAsync(receipt, ct);

        heldCredit.AmountRemaining = 0;
        heldCredit.Status = ReceiptHeldCreditStatusCodes.Released;
        heldCredit.UpdatedAt = now;
        heldCredit.Version += 1;

        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            "RECEIPT_HELD_CREDIT_RELEASE",
            "ReceiptHeldCredit",
            heldCredit.Id.ToString(),
            new
            {
                amountRemaining = releasedAmount,
                status = previousHeldStatus
            },
            new
            {
                releasedAmount,
                amountRemaining = heldCredit.AmountRemaining,
                status = heldCredit.Status,
                receiptId = receipt.Id
            },
            ct);

        return new ReceiptHeldCreditReleaseResult(
            heldCredit.Id,
            heldCredit.Version,
            heldCredit.Status,
            receipt.Id,
            releasedAmount,
            heldCredit.AmountRemaining,
            receipt.UnallocatedAmount);
    }

    private async Task EnsureCanManageCustomer(
        string customerTaxCode,
        CancellationToken ct)
    {
        var userId = _currentUser.EnsureUser();
        if (_currentUser.HasAnyRole("Admin", "Supervisor"))
        {
            return;
        }

        if (_currentUser.HasAnyRole("Accountant"))
        {
            var ownerId = await _db.Customers
                .AsNoTracking()
                .Where(c => c.TaxCode == customerTaxCode)
                .Select(c => c.AccountantOwnerId)
                .FirstOrDefaultAsync(ct);

            if (ownerId.HasValue && ownerId.Value == userId)
            {
                return;
            }
        }

        throw new UnauthorizedAccessException("Not allowed to manage this customer.");
    }

    private async Task<string> ResolveReceiptAllocationStatusAsync(Receipt receipt, CancellationToken ct)
    {
        if (receipt.Status == ReceiptStatusCodes.Void)
        {
            return ReceiptStatusCodes.Void;
        }

        var hasAllocations = await _db.ReceiptAllocations
            .AsNoTracking()
            .AnyAsync(item => item.ReceiptId == receipt.Id, ct);

        if (receipt.UnallocatedAmount > 0)
        {
            return hasAllocations
                ? ReceiptAllocationStatusCodes.Partial
                : ReceiptAllocationStatusCodes.Unallocated;
        }

        return ReceiptAllocationStatusCodes.Allocated;
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
