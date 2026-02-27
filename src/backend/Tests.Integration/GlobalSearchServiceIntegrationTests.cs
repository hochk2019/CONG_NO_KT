using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public sealed class GlobalSearchServiceIntegrationTests
{
    private readonly TestDatabaseFixture _fixture;

    public GlobalSearchServiceIntegrationTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SearchAsync_ReturnsCustomerInvoiceAndReceiptGroups()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var now = DateTimeOffset.UtcNow;
        var seller = SeedSeller(now);
        var customer = SeedCustomer("CUST-SEARCH-001", "Khach Hang Search", now);
        var invoice = SeedInvoice(
            seller.SellerTaxCode,
            customer.TaxCode,
            "INV-SEARCH-001",
            deletedAt: null,
            now);
        var receipt = SeedReceipt(
            seller.SellerTaxCode,
            customer.TaxCode,
            "PT-SEARCH-001",
            deletedAt: null,
            now);

        db.Sellers.Add(seller);
        db.Customers.Add(customer);
        db.Invoices.Add(invoice);
        db.Receipts.Add(receipt);
        await db.SaveChangesAsync();

        var service = new GlobalSearchService(db);
        var result = await service.SearchAsync("SEARCH-001", 10, CancellationToken.None);

        Assert.Equal("SEARCH-001", result.Query);
        Assert.Equal(3, result.Total);
        Assert.Contains(result.Customers, item => item.TaxCode == customer.TaxCode);
        Assert.Contains(result.Invoices, item => item.InvoiceNo == invoice.InvoiceNo);
        Assert.Contains(result.Receipts, item => item.ReceiptNo == receipt.ReceiptNo);
    }

    [Fact]
    public async Task SearchAsync_ExcludesSoftDeletedInvoicesAndReceipts()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var now = DateTimeOffset.UtcNow;
        var seller = SeedSeller(now);
        var customer = SeedCustomer("CUST-SEARCH-002", "Khach Hang Deleted", now);

        db.Sellers.Add(seller);
        db.Customers.Add(customer);

        db.Invoices.Add(SeedInvoice(
            seller.SellerTaxCode,
            customer.TaxCode,
            "INV-KEEP-001",
            deletedAt: null,
            now));
        db.Invoices.Add(SeedInvoice(
            seller.SellerTaxCode,
            customer.TaxCode,
            "INV-KEEP-999",
            deletedAt: now,
            now));

        db.Receipts.Add(SeedReceipt(
            seller.SellerTaxCode,
            customer.TaxCode,
            "PT-KEEP-001",
            deletedAt: null,
            now));
        db.Receipts.Add(SeedReceipt(
            seller.SellerTaxCode,
            customer.TaxCode,
            "PT-KEEP-999",
            deletedAt: now,
            now));

        await db.SaveChangesAsync();

        var service = new GlobalSearchService(db);
        var result = await service.SearchAsync("KEEP", 10, CancellationToken.None);

        Assert.Contains(result.Invoices, item => item.InvoiceNo == "INV-KEEP-001");
        Assert.DoesNotContain(result.Invoices, item => item.InvoiceNo == "INV-KEEP-999");

        Assert.Contains(result.Receipts, item => item.ReceiptNo == "PT-KEEP-001");
        Assert.DoesNotContain(result.Receipts, item => item.ReceiptNo == "PT-KEEP-999");
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.receipt_allocations, " +
            "congno.receipts, " +
            "congno.invoices, " +
            "congno.advances, " +
            "congno.customers, " +
            "congno.sellers " +
            "RESTART IDENTITY CASCADE;");
    }

    private static Seller SeedSeller(DateTimeOffset now)
    {
        return new Seller
        {
            SellerTaxCode = "SELLER-SEARCH",
            Name = "Seller Search",
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        };
    }

    private static Customer SeedCustomer(string taxCode, string name, DateTimeOffset now)
    {
        return new Customer
        {
            TaxCode = taxCode,
            Name = name,
            Status = "ACTIVE",
            PaymentTermsDays = 30,
            CurrentBalance = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        };
    }

    private static Invoice SeedInvoice(
        string sellerTaxCode,
        string customerTaxCode,
        string invoiceNo,
        DateTimeOffset? deletedAt,
        DateTimeOffset now)
    {
        return new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            InvoiceNo = invoiceNo,
            IssueDate = DateOnly.FromDateTime(now.UtcDateTime.Date),
            RevenueExclVat = 1_000_000m,
            VatAmount = 100_000m,
            TotalAmount = 1_100_000m,
            OutstandingAmount = 1_100_000m,
            InvoiceType = "GTGT",
            Status = "OPEN",
            DeletedAt = deletedAt,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        };
    }

    private static Receipt SeedReceipt(
        string sellerTaxCode,
        string customerTaxCode,
        string receiptNo,
        DateTimeOffset? deletedAt,
        DateTimeOffset now)
    {
        return new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            ReceiptNo = receiptNo,
            ReceiptDate = DateOnly.FromDateTime(now.UtcDateTime.Date),
            Amount = 500_000m,
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationStatus = "UNALLOCATED",
            AllocationPriority = "ISSUE_DATE",
            UnallocatedAmount = 500_000m,
            Status = "APPROVED",
            DeletedAt = deletedAt,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        };
    }
}
