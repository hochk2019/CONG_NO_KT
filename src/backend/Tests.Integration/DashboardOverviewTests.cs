using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Dashboard;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public sealed class DashboardOverviewTests
{
    private readonly TestDatabaseFixture _fixture;

    public DashboardOverviewTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Overview_ReturnsUnallocatedReceiptsAndAllocationSummary()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var (seller, customer) = await SeedMasterAsync(db);
        await SeedReceiptAsync(db, seller.SellerTaxCode, customer.TaxCode, "ALLOCATED", 1_000_000, 0);
        await SeedReceiptAsync(db, seller.SellerTaxCode, customer.TaxCode, "PARTIAL", 2_000_000, 400_000);
        await SeedReceiptAsync(db, seller.SellerTaxCode, customer.TaxCode, "UNALLOCATED", 1_500_000, 1_500_000);
        var invoiceId = await SeedInvoiceAsync(db, seller.SellerTaxCode, customer.TaxCode, 500_000);
        await SeedFullyAllocatedReceiptAsync(db, seller.SellerTaxCode, customer.TaxCode, invoiceId, 500_000);

        DapperTypeHandlers.Register();
        var currentUser = new TestCurrentUser(new[] { "Admin" });
        var service = new DashboardService(new NpgsqlConnectionFactory(_fixture.ConnectionString), currentUser);

        var result = await service.GetOverviewAsync(
            new DashboardOverviewRequest(null, null, 1, 5, null, null),
            CancellationToken.None);

        Assert.Equal(1_900_000, result.Kpis.UnallocatedReceiptsAmount);
        Assert.Equal(2, result.Kpis.UnallocatedReceiptsCount);
        Assert.Equal(1, result.Kpis.OnTimeCustomers);
        Assert.Contains(result.AllocationStatuses, item => item.Status == "ALLOCATED");
        Assert.Contains(result.AllocationStatuses, item => item.Status == "PARTIAL");
        Assert.Contains(result.AllocationStatuses, item => item.Status == "UNALLOCATED");
        Assert.Contains(result.TopOnTime, item => item.CustomerTaxCode == customer.TaxCode);
    }

    [Fact]
    public async Task Overview_Trend_UsesApprovedAt_ForReceipts()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var (seller, customer) = await SeedMasterAsync(db);

        db.Receipts.Add(new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller.SellerTaxCode,
            CustomerTaxCode = customer.TaxCode,
            ReceiptDate = new DateOnly(2025, 1, 10),
            Amount = 1_200_000,
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationStatus = "ALLOCATED",
            AllocationPriority = "ISSUE_DATE",
            Status = "APPROVED",
            UnallocatedAmount = 0,
            ApprovedAt = new DateTimeOffset(2025, 2, 5, 0, 0, 0, TimeSpan.Zero),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();

        DapperTypeHandlers.Register();
        var currentUser = new TestCurrentUser(new[] { "Admin" });
        var service = new DashboardService(new NpgsqlConnectionFactory(_fixture.ConnectionString), currentUser);

        var result = await service.GetOverviewAsync(
            new DashboardOverviewRequest(
                new DateOnly(2025, 1, 1),
                new DateOnly(2025, 2, 28),
                null,
                5,
                null,
                null),
            CancellationToken.None);

        var january = result.Trend.Single(point => point.Period == "2025-01");
        var february = result.Trend.Single(point => point.Period == "2025-02");

        Assert.Equal(0, january.ReceiptedTotal);
        Assert.Equal(1_200_000, february.ReceiptedTotal);
    }

    [Fact]
    public async Task Overview_Trend_RespectsWeeklyGranularity()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var (seller, customer) = await SeedMasterAsync(db);

        db.Receipts.Add(new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller.SellerTaxCode,
            CustomerTaxCode = customer.TaxCode,
            ReceiptDate = new DateOnly(2025, 2, 12),
            Amount = 900_000,
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationStatus = "ALLOCATED",
            AllocationPriority = "ISSUE_DATE",
            Status = "APPROVED",
            UnallocatedAmount = 0,
            ApprovedAt = new DateTimeOffset(2025, 2, 12, 0, 0, 0, TimeSpan.Zero),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();

        DapperTypeHandlers.Register();
        var currentUser = new TestCurrentUser(new[] { "Admin" });
        var service = new DashboardService(new NpgsqlConnectionFactory(_fixture.ConnectionString), currentUser);

        var result = await service.GetOverviewAsync(
            new DashboardOverviewRequest(
                new DateOnly(2025, 2, 3),
                new DateOnly(2025, 2, 14),
                null,
                5,
                "week",
                2),
            CancellationToken.None);

        Assert.Equal(2, result.Trend.Count);
        Assert.Equal(2, result.Trend.Select(point => point.Period).Distinct().Count());
        Assert.Equal(900_000, result.Trend.Sum(point => point.ReceiptedTotal));
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
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Sellers.Add(seller);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return (seller, customer);
    }

    private static async Task SeedReceiptAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        string allocationStatus,
        decimal amount,
        decimal unallocatedAmount)
    {
        db.Receipts.Add(new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Amount = amount,
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationStatus = allocationStatus,
            AllocationPriority = "ISSUE_DATE",
            Status = "APPROVED",
            UnallocatedAmount = unallocatedAmount,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedInvoiceAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        decimal amount)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            InvoiceNo = "INV-TEST",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            RevenueExclVat = amount,
            VatAmount = 0,
            TotalAmount = amount,
            OutstandingAmount = 0,
            InvoiceType = "SALE",
            Status = "APPROVED",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        return invoice.Id;
    }

    private static async Task SeedFullyAllocatedReceiptAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        Guid invoiceId,
        decimal amount)
    {
        var receiptId = Guid.NewGuid();
        db.Receipts.Add(new Receipt
        {
            Id = receiptId,
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Amount = amount,
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationStatus = "ALLOCATED",
            AllocationPriority = "ISSUE_DATE",
            Status = "APPROVED",
            UnallocatedAmount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        db.ReceiptAllocations.Add(new ReceiptAllocation
        {
            Id = Guid.NewGuid(),
            ReceiptId = receiptId,
            TargetType = "INVOICE",
            InvoiceId = invoiceId,
            Amount = amount,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(IReadOnlyList<string> roles)
        {
            Roles = roles;
        }

        public Guid? UserId => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string? Username => "test";
        public IReadOnlyList<string> Roles { get; }
        public string? IpAddress => "127.0.0.1";
    }
}
