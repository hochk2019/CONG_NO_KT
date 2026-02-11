using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Dashboard;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public sealed class DashboardOverdueGroupTests
{
    private readonly TestDatabaseFixture _fixture;

    public DashboardOverdueGroupTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task OverdueGroups_ByCustomer_ReturnsCustomerKey()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var (seller, customer) = await SeedMasterAsync(db);
        await SeedInvoiceAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            1_000_000,
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-10)));

        DapperTypeHandlers.Register();
        var currentUser = new TestCurrentUser(new[] { "Admin" });
        var service = new DashboardService(new NpgsqlConnectionFactory(_fixture.ConnectionString), currentUser);

        var result = await service.GetOverdueGroupsAsync(
            new DashboardOverdueGroupRequest(
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                5,
                "customer"),
            CancellationToken.None);

        Assert.Contains(result, row => row.GroupKey == customer.TaxCode);
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
        decimal amount,
        DateOnly issueDate)
    {
        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            InvoiceNo = "INV-OVERDUE",
            IssueDate = issueDate,
            RevenueExclVat = amount,
            VatAmount = 0,
            TotalAmount = amount,
            OutstandingAmount = amount,
            InvoiceType = "SALE",
            Status = "APPROVED",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
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
