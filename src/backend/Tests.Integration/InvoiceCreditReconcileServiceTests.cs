using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class InvoiceCreditReconcileServiceTests
{
    private readonly TestDatabaseFixture _fixture;

    public InvoiceCreditReconcileServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RunAsync_AutoAllocates_UnallocatedReceipts_To_OpenInvoices()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var (seller, customer) = await SeedMasterAsync(db);

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller.SellerTaxCode,
            CustomerTaxCode = customer.TaxCode,
            ReceiptNo = "PT-OVERPAY",
            ReceiptDate = new DateOnly(2026, 1, 20),
            Amount = 600_000m,
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationStatus = "PARTIAL",
            AllocationPriority = "ISSUE_DATE",
            UnallocatedAmount = 600_000m,
            Status = "APPROVED",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller.SellerTaxCode,
            CustomerTaxCode = customer.TaxCode,
            InvoiceTemplateCode = "01GTKT",
            InvoiceSeries = "AA/23E",
            InvoiceNo = "INV001",
            IssueDate = new DateOnly(2026, 2, 1),
            RevenueExclVat = 400_000m,
            VatAmount = 100_000m,
            TotalAmount = 500_000m,
            OutstandingAmount = 500_000m,
            InvoiceType = "NORMAL",
            Status = "OPEN",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Receipts.Add(receipt);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        using var loggerFactory = LoggerFactory.Create(builder => { });
        var service = new InvoiceCreditReconcileService(db, loggerFactory.CreateLogger<InvoiceCreditReconcileService>());

        await service.RunAsync(CancellationToken.None);

        var updatedInvoice = await db.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoice.Id);
        var updatedReceipt = await db.Receipts.AsNoTracking().FirstAsync(r => r.Id == receipt.Id);
        var allocations = await db.ReceiptAllocations.AsNoTracking()
            .Where(a => a.InvoiceId == invoice.Id)
            .ToListAsync();

        Assert.Single(allocations);
        Assert.Equal(500_000m, allocations[0].Amount);
        Assert.Equal("PAID", updatedInvoice.Status);
        Assert.Equal(0m, updatedInvoice.OutstandingAmount);
        Assert.Equal(100_000m, updatedReceipt.UnallocatedAmount);
        Assert.Equal("PARTIAL", updatedReceipt.AllocationStatus);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.receipt_allocations, " +
            "congno.receipts, " +
            "congno.invoices, " +
            "congno.customers, " +
            "congno.sellers " +
            "RESTART IDENTITY CASCADE;");
    }

    private static async Task<(Seller seller, Customer customer)> SeedMasterAsync(ConGNoDbContext db)
    {
        var seller = new Seller
        {
            SellerTaxCode = "SELLER01",
            Name = "Seller 01",
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };
        var customer = new Customer
        {
            TaxCode = "CUST01",
            Name = "Customer 01",
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Sellers.Add(seller);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return (seller, customer);
    }
}
