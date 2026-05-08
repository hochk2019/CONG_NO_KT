using CongNoGolden.Domain.Allocation;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReceiptService
{
    private async Task ApplyAllocations(
        Receipt receipt,
        IReadOnlyList<AllocationLine> lines,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var invoiceIds = lines
            .Where(line => line.TargetType == AllocationTargetType.Invoice)
            .Select(line => line.TargetId)
            .Distinct()
            .ToList();
        var advanceIds = lines
            .Where(line => line.TargetType == AllocationTargetType.Advance)
            .Select(line => line.TargetId)
            .Distinct()
            .ToList();

        var invoices = invoiceIds.Count == 0
            ? new Dictionary<Guid, Invoice>()
            : await _db.Invoices.Where(invoice => invoiceIds.Contains(invoice.Id)).ToDictionaryAsync(invoice => invoice.Id, ct);
        var advances = advanceIds.Count == 0
            ? new Dictionary<Guid, Advance>()
            : await _db.Advances.Where(advance => advanceIds.Contains(advance.Id)).ToDictionaryAsync(advance => advance.Id, ct);

        foreach (var line in lines)
        {
            if (line.TargetType == AllocationTargetType.Invoice &&
                invoices.TryGetValue(line.TargetId, out var invoice))
            {
                ApplyInvoiceAllocation(invoice, line.Amount, now);
            }

            if (line.TargetType == AllocationTargetType.Advance &&
                advances.TryGetValue(line.TargetId, out var advance))
            {
                ApplyAdvanceAllocation(advance, line.Amount, now);
            }

            _db.ReceiptAllocations.Add(new ReceiptAllocation
            {
                Id = Guid.NewGuid(),
                ReceiptId = receipt.Id,
                TargetType = line.TargetType == AllocationTargetType.Invoice ? "INVOICE" : "ADVANCE",
                InvoiceId = line.TargetType == AllocationTargetType.Invoice ? line.TargetId : null,
                AdvanceId = line.TargetType == AllocationTargetType.Advance ? line.TargetId : null,
                Amount = line.Amount,
                CreatedAt = now
            });
        }
    }

    private static void ApplyInvoiceAllocation(Invoice invoice, decimal amount, DateTimeOffset now)
    {
        invoice.OutstandingAmount = Math.Max(0, invoice.OutstandingAmount - amount);
        invoice.Status = invoice.OutstandingAmount == 0 ? "PAID" : "PARTIAL";
        TouchVersionedEntity(invoice, now);
    }

    private static void ApplyAdvanceAllocation(Advance advance, decimal amount, DateTimeOffset now)
    {
        advance.OutstandingAmount = Math.Max(0, advance.OutstandingAmount - amount);
        advance.Status = advance.OutstandingAmount == 0 ? "PAID" : "APPROVED";
        TouchVersionedEntity(advance, now);
    }

    private static void RestoreInvoice(Invoice invoice, decimal amount, DateTimeOffset now)
    {
        invoice.OutstandingAmount = Math.Min(invoice.TotalAmount, invoice.OutstandingAmount + amount);
        if (invoice.OutstandingAmount <= 0)
        {
            invoice.Status = "PAID";
        }
        else if (invoice.OutstandingAmount >= invoice.TotalAmount)
        {
            invoice.Status = "OPEN";
        }
        else
        {
            invoice.Status = "PARTIAL";
        }

        TouchVersionedEntity(invoice, now);
    }

    private static void RestoreAdvance(Advance advance, decimal amount, DateTimeOffset now)
    {
        advance.OutstandingAmount = Math.Min(advance.Amount, advance.OutstandingAmount + amount);
        advance.Status = advance.OutstandingAmount == 0 ? "PAID" : "APPROVED";
        TouchVersionedEntity(advance, now);
    }

    private static void AdjustCustomerBalance(Customer customer, decimal balanceDelta, DateTimeOffset now)
    {
        customer.CurrentBalance += balanceDelta;
        TouchVersionedEntity(customer, now);
    }

    private static void TouchVersionedEntity(Customer customer, DateTimeOffset now)
    {
        customer.UpdatedAt = now;
        customer.Version += 1;
    }

    private static void TouchVersionedEntity(Invoice invoice, DateTimeOffset now)
    {
        invoice.UpdatedAt = now;
        invoice.Version += 1;
    }

    private static void TouchVersionedEntity(Advance advance, DateTimeOffset now)
    {
        advance.UpdatedAt = now;
        advance.Version += 1;
    }
}
