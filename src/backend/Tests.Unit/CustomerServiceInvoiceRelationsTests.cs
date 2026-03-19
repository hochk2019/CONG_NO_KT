using System.Collections;
using CongNoGolden.Application.Customers;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests.Unit;

public sealed class CustomerServiceInvoiceRelationsTests
{
    [Fact]
    public async Task ListInvoicesAsync_ReturnsReductionLinks_ForBothReducedAndReductionInvoices()
    {
        await using var db = CreateDbContext(nameof(ListInvoicesAsync_ReturnsReductionLinks_ForBothReducedAndReductionInvoices));
        var now = new DateTimeOffset(2026, 3, 18, 8, 0, 0, TimeSpan.Zero);
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        db.Sellers.Add(new Seller
        {
            SellerTaxCode = "SELLER-REL",
            Name = "Seller Relation",
            ShortName = "Seller Rel",
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        });

        db.Customers.Add(new Customer
        {
            TaxCode = "CUST-REL",
            Name = "Customer Relation",
            Status = "ACTIVE",
            CurrentBalance = 0m,
            PaymentTermsDays = 30,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        });

        var reducedInvoiceId = Guid.NewGuid();
        var reductionInvoiceId = Guid.NewGuid();

        db.Invoices.AddRange(
            new Invoice
            {
                Id = reducedInvoiceId,
                SellerTaxCode = "SELLER-REL",
                CustomerTaxCode = "CUST-REL",
                InvoiceNo = "INV-OPEN-001",
                IssueDate = today.AddDays(-7),
                RevenueExclVat = 909.09m,
                VatAmount = 90.91m,
                TotalAmount = 1_000m,
                OutstandingAmount = 800m,
                InvoiceType = "NORMAL",
                Status = "OPEN",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            },
            new Invoice
            {
                Id = reductionInvoiceId,
                SellerTaxCode = "SELLER-REL",
                CustomerTaxCode = "CUST-REL",
                InvoiceNo = "INV-RED-001",
                IssueDate = today,
                RevenueExclVat = -181.82m,
                VatAmount = -18.18m,
                TotalAmount = -200m,
                OutstandingAmount = 0m,
                InvoiceType = "ADJUSTMENT_REDUCTION",
                Status = "PAID",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            });

        db.InvoiceReductionApplications.Add(new InvoiceReductionApplication
        {
            Id = Guid.NewGuid(),
            ReductionInvoiceId = reductionInvoiceId,
            AppliedInvoiceId = reducedInvoiceId,
            Amount = 200m,
            CreatedAt = now
        });

        await db.SaveChangesAsync();

        var service = new CustomerService(db, () => now.UtcDateTime);
        var result = await service.ListInvoicesAsync(
            "CUST-REL",
            new CustomerRelationRequest(null, null, null, null, null, null, 1, 20),
            CancellationToken.None);

        Assert.Equal(2, result.Items.Count);

        var reductionInvoiceRefsProperty = typeof(CustomerInvoiceDto).GetProperty("ReductionInvoiceRefs");
        var reducedInvoiceRefsProperty = typeof(CustomerInvoiceDto).GetProperty("ReducedInvoiceRefs");

        Assert.NotNull(reductionInvoiceRefsProperty);
        Assert.NotNull(reducedInvoiceRefsProperty);

        var reducedInvoice = result.Items.Single(item => item.InvoiceNo == "INV-OPEN-001");
        var reductionInvoice = result.Items.Single(item => item.InvoiceNo == "INV-RED-001");

        var reductionInvoiceRefs = ReadInvoiceRefs(reductionInvoiceRefsProperty!.GetValue(reducedInvoice));
        var reducedInvoiceRefs = ReadInvoiceRefs(reducedInvoiceRefsProperty!.GetValue(reductionInvoice));

        var reductionRef = Assert.Single(reductionInvoiceRefs);
        Assert.Equal(reductionInvoiceId, reductionRef.Id);
        Assert.Equal("INV-RED-001", reductionRef.InvoiceNo);
        Assert.Equal(today, reductionRef.IssueDate);
        Assert.Equal(200m, reductionRef.Amount);

        var reducedRef = Assert.Single(reducedInvoiceRefs);
        Assert.Equal(reducedInvoiceId, reducedRef.Id);
        Assert.Equal("INV-OPEN-001", reducedRef.InvoiceNo);
        Assert.Equal(today.AddDays(-7), reducedRef.IssueDate);
        Assert.Equal(200m, reducedRef.Amount);
    }

    private static IReadOnlyList<InvoiceRefSnapshot> ReadInvoiceRefs(object? value)
    {
        if (value is not IEnumerable enumerable)
        {
            return Array.Empty<InvoiceRefSnapshot>();
        }

        var refs = new List<InvoiceRefSnapshot>();
        foreach (var item in enumerable)
        {
            if (item is null)
            {
                continue;
            }

            var itemType = item.GetType();
            refs.Add(new InvoiceRefSnapshot(
                (Guid)(itemType.GetProperty("Id")?.GetValue(item) ?? Guid.Empty),
                (string?)itemType.GetProperty("InvoiceNo")?.GetValue(item) ?? string.Empty,
                (DateOnly)(itemType.GetProperty("IssueDate")?.GetValue(item) ?? default(DateOnly)),
                (decimal)(itemType.GetProperty("Amount")?.GetValue(item) ?? 0m)));
        }

        return refs;
    }

    private static ConGNoDbContext CreateDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"customer-invoice-relations-{name}")
            .Options;

        return new ConGNoDbContext(options);
    }

    private sealed record InvoiceRefSnapshot(Guid Id, string InvoiceNo, DateOnly IssueDate, decimal Amount);
}
