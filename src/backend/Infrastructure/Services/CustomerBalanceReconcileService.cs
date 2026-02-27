using CongNoGolden.Application.Customers;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed class CustomerBalanceReconcileService : ICustomerBalanceReconcileService
{
    private const int DefaultMaxItems = 20;
    private const decimal DefaultTolerance = 0.01m;
    private const int ReconcileBatchSize = 500;

    private readonly ConGNoDbContext _db;

    public CustomerBalanceReconcileService(ConGNoDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerBalanceReconcileResult> ReconcileAsync(CustomerBalanceReconcileRequest request, CancellationToken ct)
    {
        var normalized = Normalize(request);
        var now = DateTimeOffset.UtcNow;
        _db.ChangeTracker.Clear();

        var invoiceTotals = await LoadInvoiceTotalsAsync(ct);
        var advanceTotals = await LoadAdvanceTotalsAsync(ct);
        var receiptTotals = await LoadReceiptTotalsAsync(ct);

        var checkedCustomers = 0;
        var driftedCustomers = 0;
        var updatedCustomers = 0;
        var totalAbsoluteDrift = 0m;
        var maxAbsoluteDrift = 0m;
        var topDrifts = new List<CustomerBalanceDriftItem>(normalized.MaxItems);

        var offset = 0;
        while (true)
        {
            var customersBatch = await _db.Customers
                .AsNoTracking()
                .OrderBy(c => c.TaxCode)
                .Skip(offset)
                .Take(ReconcileBatchSize)
                .ToListAsync(ct);

            if (customersBatch.Count == 0)
            {
                break;
            }

            checkedCustomers += customersBatch.Count;
            offset += customersBatch.Count;
            var updatesInBatch = 0;
            foreach (var customer in customersBatch)
            {
                var expected = ResolveExpectedBalance(customer.TaxCode, invoiceTotals, advanceTotals, receiptTotals);
                var absoluteDrift = Math.Abs(customer.CurrentBalance - expected);
                if (absoluteDrift <= normalized.Tolerance)
                {
                    continue;
                }

                driftedCustomers++;
                totalAbsoluteDrift += absoluteDrift;
                if (absoluteDrift > maxAbsoluteDrift)
                {
                    maxAbsoluteDrift = absoluteDrift;
                }

                var driftItem = new CustomerBalanceDriftItem(
                    customer.TaxCode,
                    customer.CurrentBalance,
                    expected,
                    absoluteDrift);
                PushTopDrift(topDrifts, driftItem, normalized.MaxItems);

                if (!normalized.ApplyChanges)
                {
                    continue;
                }

                var trackedCustomer = new Customer
                {
                    TaxCode = customer.TaxCode,
                    CurrentBalance = expected,
                    UpdatedAt = now,
                    Version = customer.Version + 1
                };

                _db.Customers.Attach(trackedCustomer);
                var entry = _db.Entry(trackedCustomer);
                entry.Property(x => x.CurrentBalance).IsModified = true;
                entry.Property(x => x.UpdatedAt).IsModified = true;
                entry.Property(x => x.Version).IsModified = true;
                updatesInBatch++;
                updatedCustomers++;
            }

            if (normalized.ApplyChanges && updatesInBatch > 0)
            {
                await _db.SaveChangesAsync(ct);
                _db.ChangeTracker.Clear();
            }
        }

        var orderedTopDrifts = topDrifts
            .OrderByDescending(d => d.AbsoluteDrift)
            .ThenBy(d => d.TaxCode, StringComparer.Ordinal)
            .ToList();

        return new CustomerBalanceReconcileResult(
            now,
            CheckedCustomers: checkedCustomers,
            DriftedCustomers: driftedCustomers,
            UpdatedCustomers: updatedCustomers,
            TotalAbsoluteDrift: totalAbsoluteDrift,
            MaxAbsoluteDrift: maxAbsoluteDrift,
            TopDrifts: orderedTopDrifts);
    }

    private static NormalizedRequest Normalize(CustomerBalanceReconcileRequest request)
    {
        var maxItems = request.MaxItems <= 0 ? DefaultMaxItems : Math.Min(request.MaxItems, 200);
        var tolerance = request.Tolerance < 0 ? DefaultTolerance : request.Tolerance;
        return new NormalizedRequest(request.ApplyChanges, maxItems, tolerance);
    }

    private static decimal ResolveExpectedBalance(
        string taxCode,
        IReadOnlyDictionary<string, decimal> invoiceTotals,
        IReadOnlyDictionary<string, decimal> advanceTotals,
        IReadOnlyDictionary<string, decimal> receiptTotals)
    {
        invoiceTotals.TryGetValue(taxCode, out var invoiceTotal);
        advanceTotals.TryGetValue(taxCode, out var advanceTotal);
        receiptTotals.TryGetValue(taxCode, out var receiptTotal);
        return invoiceTotal + advanceTotal - receiptTotal;
    }

    private static void PushTopDrift(
        List<CustomerBalanceDriftItem> topDrifts,
        CustomerBalanceDriftItem candidate,
        int maxItems)
    {
        if (maxItems <= 0)
        {
            return;
        }

        if (topDrifts.Count < maxItems)
        {
            topDrifts.Add(candidate);
            return;
        }

        var smallestIndex = 0;
        for (var i = 1; i < topDrifts.Count; i++)
        {
            if (topDrifts[i].AbsoluteDrift < topDrifts[smallestIndex].AbsoluteDrift)
            {
                smallestIndex = i;
            }
        }

        if (candidate.AbsoluteDrift > topDrifts[smallestIndex].AbsoluteDrift)
        {
            topDrifts[smallestIndex] = candidate;
        }
    }

    private async Task<IReadOnlyDictionary<string, decimal>> LoadInvoiceTotalsAsync(CancellationToken ct)
    {
        return await _db.Invoices
            .AsNoTracking()
            .Where(i => i.DeletedAt == null && i.Status != "VOID")
            .GroupBy(i => i.CustomerTaxCode)
            .Select(g => new { g.Key, Total = g.Sum(x => x.TotalAmount) })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);
    }

    private async Task<IReadOnlyDictionary<string, decimal>> LoadAdvanceTotalsAsync(CancellationToken ct)
    {
        return await _db.Advances
            .AsNoTracking()
            .Where(a => a.DeletedAt == null && (a.Status == "APPROVED" || a.Status == "PAID"))
            .GroupBy(a => a.CustomerTaxCode)
            .Select(g => new { g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);
    }

    private async Task<IReadOnlyDictionary<string, decimal>> LoadReceiptTotalsAsync(CancellationToken ct)
    {
        return await _db.Receipts
            .AsNoTracking()
            .Where(r => r.DeletedAt == null && r.Status == "APPROVED")
            .GroupBy(r => r.CustomerTaxCode)
            .Select(g => new { g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);
    }

    private sealed record NormalizedRequest(bool ApplyChanges, int MaxItems, decimal Tolerance);
}
