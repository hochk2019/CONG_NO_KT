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

        await SeedInvoiceAsync(db, seller.SellerTaxCode, customer.TaxCode, "INV-OK", asOf.AddDays(-10), 1000, "OPEN");
        await SeedInvoiceAsync(db, seller.SellerTaxCode, customer.TaxCode, "INV-VOID", asOf.AddDays(-5), 500, "VOID");
        await SeedInvoiceAsync(db, seller.SellerTaxCode, customer.TaxCode, "INV-FUTURE", asOf.AddDays(5), 700, "OPEN");
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

    [Fact]
    public async Task Aging_Subtracts_Imported_Reduction_Invoices()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var (seller, customer) = await SeedMasterAsync(db);
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var originalId = Guid.NewGuid();
        var reductionId = Guid.NewGuid();

        db.Invoices.AddRange(
            new Invoice
            {
                Id = originalId,
                SellerTaxCode = seller.SellerTaxCode,
                CustomerTaxCode = customer.TaxCode,
                InvoiceNo = "INV-REDUCED",
                IssueDate = asOf.AddDays(-10),
                RevenueExclVat = 1_000m,
                VatAmount = 0,
                TotalAmount = 1_000m,
                OutstandingAmount = 400m,
                InvoiceType = "NORMAL",
                Status = "PARTIAL",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Version = 0
            },
            new Invoice
            {
                Id = reductionId,
                SellerTaxCode = seller.SellerTaxCode,
                CustomerTaxCode = customer.TaxCode,
                InvoiceNo = "ADJ-REDUCTION",
                IssueDate = asOf.AddDays(-1),
                RevenueExclVat = -600m,
                VatAmount = 0,
                TotalAmount = -600m,
                OutstandingAmount = 0,
                InvoiceType = "ADJUSTMENT_REDUCTION",
                Status = "PAID",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Version = 0
            });
        db.InvoiceReductionApplications.Add(new InvoiceReductionApplication
        {
            Id = Guid.NewGuid(),
            ReductionInvoiceId = reductionId,
            AppliedInvoiceId = originalId,
            Amount = 600m,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        DapperTypeHandlers.Register();
        var service = new ReportService(new NpgsqlConnectionFactory(_fixture.ConnectionString));

        var rows = await service.GetAgingAsync(
            new ReportAgingRequest(asOf, seller.SellerTaxCode, customer.TaxCode, null),
            CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(400m, row.Total);
        Assert.Equal(400m, row.Overdue);
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
            InvoiceType = "NORMAL",
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
