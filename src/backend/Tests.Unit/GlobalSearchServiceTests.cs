using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class GlobalSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_ReturnsGroupedResults_ForMatchedCustomerInvoiceAndReceipt()
    {
        await using var db = CreateContext();
        SeedCustomer(db, "ALPHA01", "Cong ty Alpha");
        SeedCustomer(db, "BETA01", "Cong ty Beta");
        SeedInvoice(db, "INV-ALPHA-001", "ALPHA01");
        SeedReceipt(db, "PT-ALPHA-001", "ALPHA01");
        await db.SaveChangesAsync();

        var service = new GlobalSearchService(db);

        var result = await service.SearchAsync("alpha", 5, CancellationToken.None);

        Assert.Equal("alpha", result.Query);
        Assert.True(result.Total >= 3);
        Assert.Contains(result.Customers, item => item.TaxCode == "ALPHA01");
        Assert.Contains(result.Invoices, item => item.InvoiceNo == "INV-ALPHA-001");
        Assert.Contains(result.Receipts, item => item.ReceiptNo == "PT-ALPHA-001");
    }

    [Fact]
    public async Task SearchAsync_HonorsTopAndPrioritizesExactInvoiceMatch()
    {
        await using var db = CreateContext();
        SeedCustomer(db, "CUST01", "Cong ty 01");
        SeedInvoice(db, "INV-100", "CUST01");
        SeedInvoice(db, "INV-100-EXT", "CUST01");
        await db.SaveChangesAsync();

        var service = new GlobalSearchService(db);

        var result = await service.SearchAsync("INV-100", 1, CancellationToken.None);

        Assert.Single(result.Invoices);
        Assert.Equal("INV-100", result.Invoices[0].InvoiceNo);
    }

    private static ConGNoDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"global-search-{Guid.NewGuid():N}")
            .Options;
        return new ConGNoDbContext(options);
    }

    private static void SeedCustomer(ConGNoDbContext db, string taxCode, string name)
    {
        var customer = new Customer
        {
            TaxCode = taxCode,
            Name = name,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };
        db.Customers.Add(customer);
        db.Entry(customer).Property("NameSearch").CurrentValue = name.ToLowerInvariant();
    }

    private static void SeedInvoice(ConGNoDbContext db, string invoiceNo, string customerTaxCode)
    {
        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = "SELLER01",
            CustomerTaxCode = customerTaxCode,
            InvoiceNo = invoiceNo,
            IssueDate = new DateOnly(2025, 1, 1),
            RevenueExclVat = 1_000_000,
            VatAmount = 100_000,
            TotalAmount = 1_100_000,
            OutstandingAmount = 900_000,
            InvoiceType = "VAT",
            Status = "OPEN",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });
    }

    private static void SeedReceipt(ConGNoDbContext db, string receiptNo, string customerTaxCode)
    {
        db.Receipts.Add(new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = "SELLER01",
            CustomerTaxCode = customerTaxCode,
            ReceiptNo = receiptNo,
            ReceiptDate = new DateOnly(2025, 1, 1),
            Amount = 500_000,
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationPriority = "ISSUE_DATE",
            AllocationStatus = "UNALLOCATED",
            UnallocatedAmount = 500_000,
            Status = "APPROVED",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });
    }
}
