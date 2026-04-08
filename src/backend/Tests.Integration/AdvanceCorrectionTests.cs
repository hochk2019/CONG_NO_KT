using System.Text.Json;
using CongNoGolden.Application.Advances;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class AdvanceCorrectionTests
{
    private readonly TestDatabaseFixture _fixture;

    public AdvanceCorrectionTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEditableFields_RecomputesOutstanding_AndWritesAudit()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var (seller, customer) = await SeedMasterAsync(db);
        var receipt = await SeedApprovedReceiptAsync(db, seller.SellerTaxCode, customer.TaxCode, 300m, "RCPT-ADV-CORR");

        var advance = new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller.SellerTaxCode,
            CustomerTaxCode = customer.TaxCode,
            AdvanceNo = "TH-APPROVED-001",
            AdvanceDate = new DateOnly(2026, 2, 1),
            Amount = 500m,
            OutstandingAmount = 200m,
            Description = "before",
            Status = "APPROVED",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Advances.Add(advance);
        await db.SaveChangesAsync();

        db.ReceiptAllocations.Add(new ReceiptAllocation
        {
            Id = Guid.NewGuid(),
            ReceiptId = receipt.Id,
            AdvanceId = advance.Id,
            TargetType = "ADVANCE",
            Amount = 300m,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(new[] { "Admin" });
        var service = new AdvanceService(db, user, new AuditService(db, user));

        var result = await service.UpdateAsync(
            advance.Id,
            new AdvanceUpdateRequest(
                AdvanceNo: "TH-APPROVED-EDIT",
                AdvanceDate: new DateOnly(2026, 2, 10),
                Amount: 650m,
                Description: "after",
                Reason: "correction reason",
                Version: advance.Version),
            CancellationToken.None);

        Assert.Equal(1, result.Version);

        var persisted = await db.Advances.AsNoTracking().FirstAsync(a => a.Id == advance.Id);
        Assert.Equal("TH-APPROVED-EDIT", persisted.AdvanceNo);
        Assert.Equal(new DateOnly(2026, 2, 10), persisted.AdvanceDate);
        Assert.Equal(650m, persisted.Amount);
        Assert.Equal(350m, persisted.OutstandingAmount);
        Assert.Equal("after", persisted.Description);
        Assert.Equal("APPROVED", persisted.Status);

        var persistedCustomer = await db.Customers.AsNoTracking().FirstAsync(c => c.TaxCode == customer.TaxCode);
        Assert.Equal(150m, persistedCustomer.CurrentBalance);

        var audit = await db.AuditLogs.AsNoTracking()
            .SingleAsync(log => log.EntityType == "Advance" && log.EntityId == advance.Id.ToString());

        Assert.Equal("ADVANCE_CORRECT", audit.Action);
        Assert.Equal(500m, ReadDecimal(audit.BeforeData, "Amount"));
        Assert.Equal(650m, ReadDecimal(audit.AfterData, "Amount"));
        Assert.Equal("correction reason", ReadString(audit.AfterData, "Reason"));
    }

    [Fact]
    public async Task UpdateAsync_ReallocatesExistingReceiptCredits_WhenCorrectionIncreasesOutstanding()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var (seller, customer) = await SeedMasterAsync(db);
        var allocatedReceipt = await SeedApprovedReceiptAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            300m,
            "RCPT-ADV-ALLOC-USED");
        var availableReceipt = await SeedApprovedReceiptAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            120m,
            "RCPT-ADV-ALLOC-FREE",
            allocationMode: "AUTO",
            allocationStatus: "UNALLOCATED",
            autoAllocateEnabled: true,
            unallocatedAmount: 120m,
            receiptDate: new DateOnly(2026, 2, 2));

        var advance = new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller.SellerTaxCode,
            CustomerTaxCode = customer.TaxCode,
            AdvanceNo = "TH-APPROVED-ALLOC",
            AdvanceDate = new DateOnly(2026, 2, 1),
            Amount = 500m,
            OutstandingAmount = 200m,
            Description = "before",
            Status = "APPROVED",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Advances.Add(advance);
        await db.SaveChangesAsync();

        db.ReceiptAllocations.Add(new ReceiptAllocation
        {
            Id = Guid.NewGuid(),
            ReceiptId = allocatedReceipt.Id,
            AdvanceId = advance.Id,
            TargetType = "ADVANCE",
            Amount = 300m,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(new[] { "Admin" });
        var service = new AdvanceService(db, user, new AuditService(db, user));

        var result = await service.UpdateAsync(
            advance.Id,
            new AdvanceUpdateRequest(
                AdvanceNo: advance.AdvanceNo,
                AdvanceDate: advance.AdvanceDate,
                Amount: 650m,
                Description: "after",
                Reason: "topup",
                Version: advance.Version),
            CancellationToken.None);

        Assert.Equal(2, result.Version);

        var persistedAdvance = await db.Advances.AsNoTracking().FirstAsync(a => a.Id == advance.Id);
        Assert.Equal(230m, persistedAdvance.OutstandingAmount);
        Assert.Equal("APPROVED", persistedAdvance.Status);

        var persistedReceipt = await db.Receipts.AsNoTracking().FirstAsync(r => r.Id == availableReceipt.Id);
        Assert.Equal(0m, persistedReceipt.UnallocatedAmount);
        Assert.Equal("ALLOCATED", persistedReceipt.AllocationStatus);

        var allocations = await db.ReceiptAllocations.AsNoTracking()
            .Where(a => a.AdvanceId == advance.Id)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, allocations.Count);
        Assert.Contains(allocations, allocation => allocation.ReceiptId == availableReceipt.Id && allocation.Amount == 120m);
    }

    [Fact]
    public async Task UpdateAsync_Rejects_WhenNewAmountIsLowerThanAllocatedTotal()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var (seller, customer) = await SeedMasterAsync(db);
        var receipt = await SeedApprovedReceiptAsync(db, seller.SellerTaxCode, customer.TaxCode, 300m, "RCPT-ADV-LOCK");

        var advance = new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller.SellerTaxCode,
            CustomerTaxCode = customer.TaxCode,
            AdvanceNo = "TH-LIMIT-001",
            AdvanceDate = new DateOnly(2026, 2, 2),
            Amount = 500m,
            OutstandingAmount = 200m,
            Status = "APPROVED",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Advances.Add(advance);
        await db.SaveChangesAsync();

        db.ReceiptAllocations.Add(new ReceiptAllocation
        {
            Id = Guid.NewGuid(),
            ReceiptId = receipt.Id,
            AdvanceId = advance.Id,
            TargetType = "ADVANCE",
            Amount = 300m,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(new[] { "Admin" });
        var service = new AdvanceService(db, user, new AuditService(db, user));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(
                advance.Id,
                new AdvanceUpdateRequest(
                    AdvanceNo: advance.AdvanceNo,
                    AdvanceDate: advance.AdvanceDate,
                    Amount: 250m,
                    Description: advance.Description,
                    Reason: "correction reason",
                    Version: advance.Version),
                CancellationToken.None));

        Assert.Equal("Advance amount cannot be lower than allocated total.", error.Message);
    }

    [Fact]
    public async Task UpdateAsync_RejectsVoidAdvance()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var (seller, customer) = await SeedMasterAsync(db);

        var advance = new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller.SellerTaxCode,
            CustomerTaxCode = customer.TaxCode,
            AdvanceNo = "TH-VOID-001",
            AdvanceDate = new DateOnly(2026, 2, 3),
            Amount = 400m,
            OutstandingAmount = 0m,
            Status = "VOID",
            DeletedAt = DateTimeOffset.UtcNow,
            DeletedBy = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 2
        };

        db.Advances.Add(advance);
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(new[] { "Admin" });
        var service = new AdvanceService(db, user, new AuditService(db, user));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(
                advance.Id,
                new AdvanceUpdateRequest(
                    AdvanceNo: "TH-VOID-EDIT",
                    AdvanceDate: advance.AdvanceDate,
                    Amount: advance.Amount,
                    Description: "edited",
                    Reason: "correction reason",
                    Version: advance.Version),
                CancellationToken.None));

        Assert.Equal("Void advances cannot be edited.", error.Message);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.audit_logs, " +
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
            CurrentBalance = 0m,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Sellers.Add(seller);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return (seller, customer);
    }

    private static async Task<Receipt> SeedApprovedReceiptAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        decimal amount,
        string receiptNo,
        string allocationMode = "MANUAL",
        string allocationStatus = "ALLOCATED",
        bool autoAllocateEnabled = false,
        decimal? unallocatedAmount = null,
        DateOnly? receiptDate = null)
    {
        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            ReceiptNo = receiptNo,
            ReceiptDate = receiptDate ?? new DateOnly(2026, 1, 20),
            Amount = amount,
            Method = "BANK",
            AllocationMode = allocationMode,
            AllocationStatus = allocationStatus,
            AllocationPriority = "ISSUE_DATE",
            AutoAllocateEnabled = autoAllocateEnabled,
            UnallocatedAmount = unallocatedAmount ?? 0m,
            Status = "APPROVED",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Receipts.Add(receipt);
        await db.SaveChangesAsync();
        return receipt;
    }

    private static decimal ReadDecimal(string? json, string propertyName)
    {
        using var document = JsonDocument.Parse(json ?? throw new InvalidOperationException("Audit payload missing."));
        return document.RootElement.GetProperty(propertyName).GetDecimal();
    }

    private static string? ReadString(string? json, string propertyName)
    {
        using var document = JsonDocument.Parse(json ?? throw new InvalidOperationException("Audit payload missing."));
        return document.RootElement.GetProperty(propertyName).GetString();
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
