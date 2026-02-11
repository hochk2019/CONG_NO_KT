using CongNoGolden.Application.Invoices;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CongNoGolden.Infrastructure.Services;

public sealed class InvoiceCreditReconcileService : IInvoiceCreditReconcileService
{
    private readonly ConGNoDbContext _db;
    private readonly ILogger<InvoiceCreditReconcileService> _logger;

    public InvoiceCreditReconcileService(ConGNoDbContext db, ILogger<InvoiceCreditReconcileService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<InvoiceCreditReconcileResult> RunAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var invoicePairs = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.DeletedAt == null && i.OutstandingAmount > 0 && i.Status != "VOID")
            .Select(i => new { i.SellerTaxCode, i.CustomerTaxCode })
            .Distinct()
            .ToListAsync(ct);

        if (invoicePairs.Count == 0)
        {
            return new InvoiceCreditReconcileResult(0, 0, 0);
        }

        var invoicesUpdated = 0;
        var receiptsUpdated = 0;
        var allocationsCreated = 0;

        foreach (var pair in invoicePairs)
        {
            try
            {
                var invoices = await _db.Invoices
                    .Where(i => i.DeletedAt == null)
                    .Where(i => i.OutstandingAmount > 0 && i.Status != "VOID")
                    .Where(i => i.SellerTaxCode == pair.SellerTaxCode && i.CustomerTaxCode == pair.CustomerTaxCode)
                    .OrderBy(i => i.IssueDate)
                    .ThenBy(i => i.CreatedAt)
                    .ToListAsync(ct);

                if (invoices.Count == 0)
                {
                    continue;
                }

                var receipts = await _db.Receipts
                    .Where(r => r.DeletedAt == null && r.Status == "APPROVED")
                    .Where(r => r.UnallocatedAmount > 0)
                    .Where(r => r.SellerTaxCode == pair.SellerTaxCode && r.CustomerTaxCode == pair.CustomerTaxCode)
                    .OrderBy(r => r.ReceiptDate)
                    .ThenBy(r => r.CreatedAt)
                    .ToListAsync(ct);

                if (receipts.Count == 0)
                {
                    continue;
                }

                foreach (var invoice in invoices)
                {
                    if (invoice.OutstandingAmount <= 0)
                    {
                        continue;
                    }

                    var remaining = invoice.OutstandingAmount;
                    var allocatedTotal = 0m;

                    foreach (var receipt in receipts)
                    {
                        if (remaining <= 0)
                        {
                            break;
                        }

                        if (receipt.UnallocatedAmount <= 0)
                        {
                            continue;
                        }

                        var allocated = Math.Min(remaining, receipt.UnallocatedAmount);
                        if (allocated <= 0)
                        {
                            continue;
                        }

                        receipt.UnallocatedAmount -= allocated;
                        receipt.AllocationStatus = receipt.UnallocatedAmount == 0 ? "ALLOCATED" : "PARTIAL";
                        receipt.UpdatedAt = now;
                        receipt.Version += 1;
                        receiptsUpdated += 1;

                        remaining -= allocated;
                        allocatedTotal += allocated;

                        _db.ReceiptAllocations.Add(new ReceiptAllocation
                        {
                            Id = Guid.NewGuid(),
                            ReceiptId = receipt.Id,
                            TargetType = "INVOICE",
                            InvoiceId = invoice.Id,
                            Amount = allocated,
                            CreatedAt = now
                        });
                        allocationsCreated += 1;
                    }

                    if (allocatedTotal <= 0)
                    {
                        continue;
                    }

                    invoice.OutstandingAmount = remaining;
                    invoice.Status = remaining == 0 ? "PAID" : "PARTIAL";
                    invoice.UpdatedAt = now;
                    invoice.Version += 1;
                    invoicesUpdated += 1;
                }

                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Invoice credit reconcile failed for seller {SellerTaxCode} customer {CustomerTaxCode}.",
                    pair.SellerTaxCode,
                    pair.CustomerTaxCode);
            }
        }

        return new InvoiceCreditReconcileResult(invoicesUpdated, receiptsUpdated, allocationsCreated);
    }
}
