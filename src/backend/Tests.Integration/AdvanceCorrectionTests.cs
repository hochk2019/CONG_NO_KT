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

        var audit = await db.AuditLogs.AsNoTracking()
            .SingleAsync(log => log.EntityType == "Advance" && log.EntityId == advance.Id.ToString());

        Assert.Equal("ADVANCE_CORRECT", audit.Action);
        Assert.Equal(500m, ReadDecimal(audit.BeforeData, "Amount"));
        Assert.Equal(650m, ReadDecimal(audit.AfterData, "Amount"));
        Assert.Equal("correction reason", ReadString(audit.AfterData, "Reason"));
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
        string receiptNo)
    {
        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            ReceiptNo = receiptNo,
            ReceiptDate = new DateOnly(2026, 1, 20),
            Amount = amount,
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationStatus = "ALLOCATED",
            AllocationPriority = "ISSUE_DATE",
            UnallocatedAmount = 0m,
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
