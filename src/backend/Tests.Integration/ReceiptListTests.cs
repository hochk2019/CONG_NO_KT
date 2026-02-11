using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class ReceiptListTests
{
    private readonly TestDatabaseFixture _fixture;

    public ReceiptListTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListAsync_FilterAllocated_ReturnsAllocatedAndPartial()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var (seller, customer) = await SeedMasterAsync(db);
        await SeedReceiptAsync(db, seller.SellerTaxCode, customer.TaxCode, "ALLOCATED");
        await SeedReceiptAsync(db, seller.SellerTaxCode, customer.TaxCode, "PARTIAL");
        await SeedReceiptAsync(db, seller.SellerTaxCode, customer.TaxCode, "UNALLOCATED");

        var user = new TestCurrentUser(new[] { "Admin" });
        var audit = new AuditService(db, user);
        var service = new ReceiptService(db, user, audit);

        var result = await service.ListAsync(
            new ReceiptListRequest(
                seller.SellerTaxCode,
                customer.TaxCode,
                null,
                "ALLOCATED",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                1,
                20),
            CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item =>
            Assert.True(item.AllocationStatus is "ALLOCATED" or "PARTIAL"));
    }

    [Fact]
    public async Task ListAsync_FilterUnallocated_ReturnsUnallocatedSelectedAndSuggested()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var (seller, customer) = await SeedMasterAsync(db);
        await SeedReceiptAsync(db, seller.SellerTaxCode, customer.TaxCode, "UNALLOCATED");
        await SeedReceiptAsync(db, seller.SellerTaxCode, customer.TaxCode, "SELECTED");
        await SeedReceiptAsync(db, seller.SellerTaxCode, customer.TaxCode, "SUGGESTED");
        await SeedReceiptAsync(db, seller.SellerTaxCode, customer.TaxCode, "ALLOCATED");

        var user = new TestCurrentUser(new[] { "Admin" });
        var audit = new AuditService(db, user);
        var service = new ReceiptService(db, user, audit);

        var result = await service.ListAsync(
            new ReceiptListRequest(
                seller.SellerTaxCode,
                customer.TaxCode,
                null,
                "UNALLOCATED",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                1,
                20),
            CancellationToken.None);

        Assert.Equal(3, result.Items.Count);
        Assert.All(result.Items, item =>
            Assert.True(item.AllocationStatus is "UNALLOCATED" or "SELECTED" or "SUGGESTED"));
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.receipt_allocations, " +
            "congno.receipts, " +
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
        string allocationStatus)
    {
        db.Receipts.Add(new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Amount = 1_000_000,
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationStatus = allocationStatus,
            AllocationPriority = "ISSUE_DATE",
            Status = "APPROVED",
            UnallocatedAmount = allocationStatus == "ALLOCATED" ? 0 : 500_000,
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
