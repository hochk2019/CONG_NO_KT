using CongNoGolden.Application.Reports;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public sealed class ReportAgingTests
{
    private readonly TestDatabaseFixture _fixture;

    public ReportAgingTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Aging_Excludes_Void_And_Future_Documents()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var (seller, customer) = await SeedMasterAsync(db);
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        await SeedInvoiceAsync(db, seller.SellerTaxCode, customer.TaxCode, "INV-OK", asOf.AddDays(-10), 1000, "APPROVED");
        await SeedInvoiceAsync(db, seller.SellerTaxCode, customer.TaxCode, "INV-VOID", asOf.AddDays(-5), 500, "VOID");
        await SeedInvoiceAsync(db, seller.SellerTaxCode, customer.TaxCode, "INV-FUTURE", asOf.AddDays(5), 700, "APPROVED");
        await SeedAdvanceAsync(db, seller.SellerTaxCode, customer.TaxCode, asOf.AddDays(5), 300, "APPROVED");

        DapperTypeHandlers.Register();
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var service = new ReportService(connectionFactory);

        var rows = await service.GetAgingAsync(
            new ReportAgingRequest(asOf, seller.SellerTaxCode, customer.TaxCode, null),
            CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(1000m, row.Total);
        Assert.Equal(1000m, row.Overdue);
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
            PaymentTermsDays = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Sellers.Add(seller);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return (seller, customer);
    }

    private static async Task SeedInvoiceAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        string invoiceNo,
        DateOnly issueDate,
        decimal amount,
        string status)
    {
        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            InvoiceNo = invoiceNo,
            IssueDate = issueDate,
            RevenueExclVat = amount,
            VatAmount = 0,
            TotalAmount = amount,
            OutstandingAmount = amount,
            InvoiceType = "SALE",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedAdvanceAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        DateOnly advanceDate,
        decimal amount,
        string status)
    {
        db.Advances.Add(new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            AdvanceNo = "ADV-01",
            AdvanceDate = advanceDate,
            Amount = amount,
            OutstandingAmount = amount,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }
}
