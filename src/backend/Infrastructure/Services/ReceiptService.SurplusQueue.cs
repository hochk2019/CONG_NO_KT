using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.StatusCodes;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReceiptService
{
    private const string UnallocatedReceiptQueueItemType = "UNALLOCATED_RECEIPT";
    private const string PartialReceiptQueueItemType = "PARTIAL_RECEIPT";
    private const string HeldCreditQueueItemType = "HELD_CREDIT";

    public async Task<PagedResult<ReceiptSurplusQueueItem>> ListSurplusQueueAsync(
        ReceiptSurplusQueueRequest request,
        CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);
        var ownerFilter = _currentUser.ResolveOwnerFilter(
            privilegedRoles: ["Admin", "Supervisor"]);
        var isAdmin = !ownerFilter.HasValue;

        var receiptItems =
            from receipt in _db.Receipts.AsNoTracking()
            join customer in _db.Customers.AsNoTracking()
                on receipt.CustomerTaxCode equals customer.TaxCode into customerJoin
            from customer in customerJoin.DefaultIfEmpty()
            where receipt.DeletedAt == null
                && receipt.Status == ReceiptStatusCodes.Approved
                && receipt.UnallocatedAmount > 0
                && (
                    receipt.AllocationStatus == ReceiptAllocationStatusCodes.Unallocated ||
                    receipt.AllocationStatus == ReceiptAllocationStatusCodes.Selected ||
                    receipt.AllocationStatus == ReceiptAllocationStatusCodes.Suggested ||
                    receipt.AllocationStatus == ReceiptAllocationStatusCodes.Partial)
            select new
            {
                Id = receipt.Id,
                ItemType = receipt.AllocationStatus == ReceiptAllocationStatusCodes.Partial
                    ? PartialReceiptQueueItemType
                    : UnallocatedReceiptQueueItemType,
                Version = receipt.Version,
                Status = receipt.AllocationStatus,
                ReceiptId = receipt.Id,
                ReceiptNo = receipt.ReceiptNo,
                ReceiptDate = receipt.ReceiptDate,
                SellerTaxCode = receipt.SellerTaxCode,
                CustomerTaxCode = receipt.CustomerTaxCode,
                CustomerName = customer != null ? customer.Name : null,
                OwnerId = customer != null ? customer.AccountantOwnerId : null,
                OriginalInvoiceNo = (string?)null,
                OriginalInvoiceDate = (DateOnly?)null,
                AmountRemaining = receipt.UnallocatedAmount,
                AgeFromDate = receipt.ReceiptDate
            };

        var heldCreditItems =
            from heldCredit in _db.ReceiptHeldCredits.AsNoTracking()
            join receipt in _db.Receipts.AsNoTracking()
                    .Where(item => item.DeletedAt == null && item.Status == ReceiptStatusCodes.Approved)
                on heldCredit.ReceiptId equals receipt.Id
            join invoice in _db.Invoices.AsNoTracking().Where(item => item.DeletedAt == null)
                on heldCredit.OriginalInvoiceId equals invoice.Id into invoiceJoin
            from invoice in invoiceJoin.DefaultIfEmpty()
            join customer in _db.Customers.AsNoTracking()
                on receipt.CustomerTaxCode equals customer.TaxCode into customerJoin
            from customer in customerJoin.DefaultIfEmpty()
            where heldCredit.AmountRemaining > 0
                && (
                    heldCredit.Status == ReceiptHeldCreditStatusCodes.Holding ||
                    heldCredit.Status == ReceiptHeldCreditStatusCodes.Partial)
            select new
            {
                Id = heldCredit.Id,
                ItemType = HeldCreditQueueItemType,
                Version = heldCredit.Version,
                Status = heldCredit.Status,
                ReceiptId = receipt.Id,
                ReceiptNo = receipt.ReceiptNo,
                ReceiptDate = receipt.ReceiptDate,
                SellerTaxCode = receipt.SellerTaxCode,
                CustomerTaxCode = receipt.CustomerTaxCode,
                CustomerName = customer != null ? customer.Name : null,
                OwnerId = customer != null ? customer.AccountantOwnerId : null,
                OriginalInvoiceNo = invoice != null ? invoice.InvoiceNo : null,
                OriginalInvoiceDate = invoice != null ? (DateOnly?)invoice.IssueDate : null,
                AmountRemaining = heldCredit.AmountRemaining,
                AgeFromDate = receipt.ReceiptDate
            };

        var query = receiptItems.Concat(heldCreditItems);

        if (ownerFilter.HasValue)
        {
            var ownerId = ownerFilter.Value;
            query = query.Where(item => item.OwnerId == ownerId);
        }

        if (!string.IsNullOrWhiteSpace(request.ItemType))
        {
            var itemType = request.ItemType.Trim().ToUpperInvariant();
            query = query.Where(item => item.ItemType == itemType);
        }

        if (!string.IsNullOrWhiteSpace(request.SellerTaxCode))
        {
            var sellerTaxCode = request.SellerTaxCode.Trim();
            query = query.Where(item => item.SellerTaxCode == sellerTaxCode);
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerTaxCode))
        {
            var customerTaxCode = request.CustomerTaxCode.Trim();
            query = query.Where(item => item.CustomerTaxCode == customerTaxCode);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(item =>
                (item.ReceiptNo != null && EF.Functions.ILike(item.ReceiptNo, pattern)) ||
                (item.OriginalInvoiceNo != null && EF.Functions.ILike(item.OriginalInvoiceNo, pattern)) ||
                EF.Functions.ILike(item.CustomerTaxCode, pattern) ||
                (item.CustomerName != null && EF.Functions.ILike(item.CustomerName, pattern)));
        }

        if (request.From.HasValue)
        {
            var from = request.From.Value;
            query = query.Where(item => item.ReceiptDate >= from);
        }

        if (request.To.HasValue)
        {
            var to = request.To.Value;
            query = query.Where(item => item.ReceiptDate <= to);
        }

        if (request.AmountMin.HasValue)
        {
            var amountMin = request.AmountMin.Value;
            query = query.Where(item => item.AmountRemaining >= amountMin);
        }

        if (request.AmountMax.HasValue)
        {
            var amountMax = request.AmountMax.Value;
            query = query.Where(item => item.AmountRemaining <= amountMax);
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(item => item.ReceiptDate)
            .ThenBy(item => item.ItemType)
            .ThenByDescending(item => item.ReceiptNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var ownerIds = rows
            .Where(item => item.OwnerId.HasValue)
            .Select(item => item.OwnerId!.Value)
            .Distinct()
            .ToList();

        var owners = ownerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users
                .Where(user => ownerIds.Contains(user.Id))
                .ToDictionaryAsync(
                    user => user.Id,
                    user => string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName,
                    ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var items = rows.Select(item =>
        {
            var ownerName = item.OwnerId.HasValue &&
                owners.TryGetValue(item.OwnerId.Value, out var name)
                ? name
                : null;

            var canManage = isAdmin || item.OwnerId == ownerFilter;
            var ageDays = Math.Max(0, today.DayNumber - item.AgeFromDate.DayNumber);

            return new ReceiptSurplusQueueItem(
                item.Id,
                item.ItemType,
                item.Version,
                item.Status,
                item.ReceiptId,
                item.ReceiptNo,
                item.ReceiptDate,
                item.SellerTaxCode,
                item.CustomerTaxCode,
                item.CustomerName,
                ownerName,
                item.OriginalInvoiceNo,
                item.OriginalInvoiceDate,
                item.AmountRemaining,
                ageDays,
                canManage);
        }).ToList();

        return new PagedResult<ReceiptSurplusQueueItem>(items, page, pageSize, total);
    }
}
