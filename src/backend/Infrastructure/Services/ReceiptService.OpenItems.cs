using CongNoGolden.Application.Receipts;
using CongNoGolden.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReceiptService
{
    public async Task<IReadOnlyList<ReceiptOpenItemDto>> ListOpenItemsAsync(
        string sellerTaxCode,
        string customerTaxCode,
        CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var seller = sellerTaxCode?.Trim() ?? string.Empty;
        var customer = customerTaxCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(seller) || string.IsNullOrWhiteSpace(customer))
        {
            throw new InvalidOperationException("Seller and customer tax code are required.");
        }

        await EnsureCanManageCustomer(customer, ct);
        return await LoadOpenItemsAsync(seller, customer, ct);
    }

    private async Task<IReadOnlyList<ReceiptOpenItemDto>> LoadOpenItemsAsync(
        string sellerTaxCode,
        string customerTaxCode,
        CancellationToken ct)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TaxCode == customerTaxCode, ct);

        if (customer is null)
        {
            throw new InvalidOperationException("Customer not found.");
        }

        var paymentTermsDays = customer.PaymentTermsDays;

        var invoiceItems = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.SellerTaxCode == sellerTaxCode && i.CustomerTaxCode == customerTaxCode && i.DeletedAt == null)
            .Where(i => i.OutstandingAmount > 0 && i.Status != "VOID")
            .Select(i => new ReceiptOpenItemDto(
                "INVOICE",
                i.Id,
                i.InvoiceNo,
                i.IssueDate,
                i.IssueDate.AddDays(paymentTermsDays),
                i.OutstandingAmount,
                i.SellerTaxCode,
                i.CustomerTaxCode))
            .ToListAsync(ct);

        var advanceItems = await _db.Advances
            .AsNoTracking()
            .Where(a => a.SellerTaxCode == sellerTaxCode && a.CustomerTaxCode == customerTaxCode && a.DeletedAt == null)
            .Where(a => a.OutstandingAmount > 0 && (a.Status == "APPROVED" || a.Status == "PAID"))
            .Select(a => new ReceiptOpenItemDto(
                "ADVANCE",
                a.Id,
                string.IsNullOrWhiteSpace(a.AdvanceNo) ? a.Id.ToString() : a.AdvanceNo,
                a.AdvanceDate,
                a.AdvanceDate.AddDays(paymentTermsDays),
                a.OutstandingAmount,
                a.SellerTaxCode,
                a.CustomerTaxCode))
            .ToListAsync(ct);

        return invoiceItems.Concat(advanceItems).ToList();
    }

    private async Task EnsureCanManageCustomer(
        string customerTaxCode,
        CancellationToken ct,
        string? unauthorizedMessage = null)
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

        throw new UnauthorizedAccessException(
            unauthorizedMessage ?? "Not allowed to manage this customer.");
    }
}
