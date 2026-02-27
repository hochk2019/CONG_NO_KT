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
        Assert.Equal(0, january.ExpectedTotal);
        Assert.Equal(0, january.ActualTotal);
        Assert.Equal(0, january.Variance);
        Assert.Equal(1_200_000, february.ReceiptedTotal);
        Assert.Equal(0, february.ExpectedTotal);
        Assert.Equal(1_200_000, february.ActualTotal);
        Assert.Equal(1_200_000, february.Variance);

        var marchForecast = Assert.Single(result.CashflowForecast.Where(point => point.Period == "2025-03"));
        Assert.Equal(0, marchForecast.ExpectedTotal);
        Assert.Equal(600_000, marchForecast.ActualTotal);
        Assert.Equal(600_000, marchForecast.Variance);
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
        Assert.Equal(4, result.CashflowForecast.Count);
        Assert.All(result.CashflowForecast, point => Assert.StartsWith("2025-W", point.Period));
    }

    [Fact]
    public async Task Overview_ReturnsExecutiveSummaryAndMomDelta()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var (seller, customer) = await SeedMasterAsync(db);
        var previousInvoiceId = await SeedInvoiceAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            2_000_000,
            new DateOnly(2025, 1, 15));

        await SeedFullyAllocatedReceiptAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            previousInvoiceId,
            2_000_000,
            new DateOnly(2025, 2, 10),
            new DateTimeOffset(2025, 2, 10, 0, 0, 0, TimeSpan.Zero));

        await SeedInvoiceAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            1_000_000,
            new DateOnly(2025, 2, 20));

        DapperTypeHandlers.Register();
        var currentUser = new TestCurrentUser(new[] { "Admin" });
        var service = new DashboardService(new NpgsqlConnectionFactory(_fixture.ConnectionString), currentUser);

        var result = await service.GetOverviewAsync(
            new DashboardOverviewRequest(
                new DateOnly(2025, 2, 1),
                new DateOnly(2025, 2, 28),
                null,
                5,
                null,
                null),
            CancellationToken.None);

        Assert.NotNull(result.ExecutiveSummary);
        Assert.False(string.IsNullOrWhiteSpace(result.ExecutiveSummary.Message));
        Assert.Equal(1_000_000, result.Kpis.TotalOutstanding);
        Assert.Equal(1_000_000, result.KpiMoM.TotalOutstanding.Current);
        Assert.Equal(2_000_000, result.KpiMoM.TotalOutstanding.Previous);
        Assert.Equal(-1_000_000, result.KpiMoM.TotalOutstanding.Delta);
        Assert.Equal(-50m, result.KpiMoM.TotalOutstanding.DeltaPercent);
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
        decimal amount,
        DateOnly? issueDate = null)
    {
        var resolvedIssueDate = issueDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            InvoiceNo = "INV-TEST",
            IssueDate = resolvedIssueDate,
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
        decimal amount,
        DateOnly? receiptDate = null,
        DateTimeOffset? approvedAt = null)
    {
        var resolvedReceiptDate = receiptDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var receiptId = Guid.NewGuid();
        db.Receipts.Add(new Receipt
        {
            Id = receiptId,
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            ReceiptDate = resolvedReceiptDate,
            Amount = amount,
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationStatus = "ALLOCATED",
            AllocationPriority = "ISSUE_DATE",
            Status = "APPROVED",
            UnallocatedAmount = 0,
            ApprovedAt = approvedAt,
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
