using CongNoGolden.Application.Advances;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class VoidReversalTests
{
    private readonly TestDatabaseFixture _fixture;

    public VoidReversalTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AdvanceList_StatusVoid_IncludesSoftDeletedVoids_Only()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);
        var (seller, customer) = await SeedMasterAsync(db);

        var voidAdvance = new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller.SellerTaxCode,
            CustomerTaxCode = customer.TaxCode,
            AdvanceNo = "TH-VOID",
            AdvanceDate = new DateOnly(2026, 2, 10),
            Amount = 300_000m,
            OutstandingAmount = 0m,
            Status = "VOID",
            DeletedAt = DateTimeOffset.UtcNow,
            DeletedBy = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 1
        };

        var activeAdvance = new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller.SellerTaxCode,
            CustomerTaxCode = customer.TaxCode,
            AdvanceNo = "TH-DRAFT",
            AdvanceDate = new DateOnly(2026, 2, 11),
            Amount = 200_000m,
            OutstandingAmount = 200_000m,
            Status = "DRAFT",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Advances.AddRange(voidAdvance, activeAdvance);
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(new[] { "Admin" });
        var service = new AdvanceService(db, user, new AuditService(db, user));

        var voidOnly = await service.ListAsync(
            new AdvanceListRequest(
                seller.SellerTaxCode,
                customer.TaxCode,
                "VOID",
                null,
                null,
                null,
                null,
                null,
                null,
                1,
                20),
            CancellationToken.None);

        Assert.Single(voidOnly.Items);
        Assert.Equal(voidAdvance.Id, voidOnly.Items[0].Id);

        var activeOnly = await service.ListAsync(
            new AdvanceListRequest(
                seller.SellerTaxCode,
                customer.TaxCode,
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

        Assert.Single(activeOnly.Items);
        Assert.Equal(activeAdvance.Id, activeOnly.Items[0].Id);
    }

    [Fact]
    public async Task ReceiptList_StatusVoid_IncludesSoftDeletedVoids_Only()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);
        var (seller, customer) = await SeedMasterAsync(db);

        var voidReceipt = new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller.SellerTaxCode,
            CustomerTaxCode = customer.TaxCode,
            ReceiptNo = "PT-VOID",
            ReceiptDate = new DateOnly(2026, 2, 12),
            Amount = 400_000m,
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationStatus = "VOID",
            AllocationPriority = "ISSUE_DATE",
            UnallocatedAmount = 0m,
            Status = "VOID",
            DeletedAt = DateTimeOffset.UtcNow,
            DeletedBy = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 1
        };

        var activeReceipt = new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller.SellerTaxCode,
            CustomerTaxCode = customer.TaxCode,
            ReceiptNo = "PT-DRAFT",
            ReceiptDate = new DateOnly(2026, 2, 13),
            Amount = 250_000m,
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationStatus = "UNALLOCATED",
            AllocationPriority = "ISSUE_DATE",
            UnallocatedAmount = 0m,
            Status = "DRAFT",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Receipts.AddRange(voidReceipt, activeReceipt);
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(new[] { "Admin" });
        var service = new ReceiptService(db, user, new AuditService(db, user));

        var voidOnly = await service.ListAsync(
            new ReceiptListRequest(
                seller.SellerTaxCode,
                customer.TaxCode,
                "VOID",
                null,
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

        Assert.Single(voidOnly.Items);
        Assert.Equal(voidReceipt.Id, voidOnly.Items[0].Id);

        var activeOnly = await service.ListAsync(
            new ReceiptListRequest(
                seller.SellerTaxCode,
                customer.TaxCode,
                null,
                null,
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

        Assert.Single(activeOnly.Items);
        Assert.Equal(activeReceipt.Id, activeOnly.Items[0].Id);
    }

    [Fact]
    public async Task AdvanceUnvoidAsync_RestoresToDraft()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);
        var (seller, customer) = await SeedMasterAsync(db);

        var advance = new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller.SellerTaxCode,
            CustomerTaxCode = customer.TaxCode,
            AdvanceNo = "TH-RESTORE",
            AdvanceDate = new DateOnly(2026, 2, 14),
            Amount = 150_000m,
            OutstandingAmount = 0m,
            Status = "VOID",
            DeletedAt = DateTimeOffset.UtcNow,
            DeletedBy = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 2
        };

        db.Advances.Add(advance);
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(new[] { "Admin" });
        var service = new AdvanceService(db, user, new AuditService(db, user));

        var restored = await service.UnvoidAsync(
            advance.Id,
            new AdvanceUnvoidRequest(advance.Version),
            CancellationToken.None);

        Assert.Equal("DRAFT", restored.Status);
        Assert.Equal(advance.Amount, restored.OutstandingAmount);

        var row = await db.Advances.AsNoTracking().FirstAsync(a => a.Id == advance.Id);
        Assert.Equal("DRAFT", row.Status);
        Assert.Null(row.DeletedAt);
        Assert.Null(row.DeletedBy);
        Assert.Equal(3, row.Version);
        Assert.Equal(advance.Amount, row.OutstandingAmount);
    }

    [Fact]
    public async Task ReceiptUnvoidAsync_RestoresToDraft_AndSelectedState()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);
        var (seller, customer) = await SeedMasterAsync(db);
        var selectedId = Guid.NewGuid();

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller.SellerTaxCode,
            CustomerTaxCode = customer.TaxCode,
            ReceiptNo = "PT-RESTORE",
            ReceiptDate = new DateOnly(2026, 2, 15),
            Amount = 500_000m,
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationStatus = "VOID",
            AllocationPriority = "ISSUE_DATE",
            AllocationTargets = $"[{{\"id\":\"{selectedId}\",\"targetType\":\"INVOICE\"}}]",
            UnallocatedAmount = 0m,
            Status = "VOID",
            DeletedAt = DateTimeOffset.UtcNow,
            DeletedBy = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 5
        };

        db.Receipts.Add(receipt);
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(new[] { "Admin" });
        var service = new ReceiptService(db, user, new AuditService(db, user));

        var restored = await service.UnvoidAsync(
            receipt.Id,
            new ReceiptUnvoidRequest(receipt.Version),
            CancellationToken.None);

        Assert.Equal("DRAFT", restored.Status);
        Assert.Equal("SELECTED", restored.AllocationStatus);
        Assert.Equal(0m, restored.UnallocatedAmount);

        var row = await db.Receipts.AsNoTracking().FirstAsync(r => r.Id == receipt.Id);
        Assert.Equal("DRAFT", row.Status);
        Assert.Equal("SELECTED", row.AllocationStatus);
        Assert.Null(row.DeletedAt);
        Assert.Null(row.DeletedBy);
        Assert.Equal(6, row.Version);
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

        public Guid? UserId => Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        public string? Username => "tester";
        public IReadOnlyList<string> Roles { get; }
        public string? IpAddress => "127.0.0.1";
    }
}
