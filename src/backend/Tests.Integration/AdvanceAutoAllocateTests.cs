using CongNoGolden.Application.Advances;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class AdvanceAutoAllocateTests
{
    private readonly TestDatabaseFixture _fixture;

    public AdvanceAutoAllocateTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ApproveAsync_AutoAllocates_FromOverpaidReceipts()
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
            AllocationStatus = "UNALLOCATED",
            AllocationPriority = "ISSUE_DATE",
            UnallocatedAmount = 600_000m,
            Status = "APPROVED",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        var advance = new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller.SellerTaxCode,
            CustomerTaxCode = customer.TaxCode,
            AdvanceNo = "TH-NEW",
            AdvanceDate = new DateOnly(2026, 2, 1),
            Amount = 500_000m,
            OutstandingAmount = 0m,
            Status = "DRAFT",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Receipts.Add(receipt);
        db.Advances.Add(advance);
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(new[] { "Admin" });
        var audit = new AuditService(db, user);
        var service = new AdvanceService(db, user, audit);

        await service.ApproveAsync(
            advance.Id,
            new AdvanceApproveRequest(advance.Version),
            CancellationToken.None);

        var updatedAdvance = await db.Advances.AsNoTracking().FirstAsync(a => a.Id == advance.Id);
        var updatedReceipt = await db.Receipts.AsNoTracking().FirstAsync(r => r.Id == receipt.Id);
        var allocations = await db.ReceiptAllocations.AsNoTracking()
            .Where(a => a.AdvanceId == advance.Id)
            .ToListAsync();

        Assert.Single(allocations);
        Assert.Equal(500_000m, allocations[0].Amount);
        Assert.Equal("PAID", updatedAdvance.Status);
        Assert.Equal(0m, updatedAdvance.OutstandingAmount);
        Assert.Equal(100_000m, updatedReceipt.UnallocatedAmount);
        Assert.Equal("PARTIAL", updatedReceipt.AllocationStatus);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.receipt_allocations, " +
            "congno.receipts, " +
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

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(IReadOnlyList<string> roles)
        {
            Roles = roles;
        }

        public Guid? UserId => Guid.Parse("33333333-3333-3333-3333-333333333333");
        public string? Username => "tester";
        public IReadOnlyList<string> Roles { get; }
        public string? IpAddress => "127.0.0.1";
    }
}
