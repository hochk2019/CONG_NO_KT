using System.Text.Json;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Imports;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Security;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class ImportCommitAdvanceAutoAllocateTests
{
    private readonly TestDatabaseFixture _fixture;

    public ImportCommitAdvanceAutoAllocateTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CommitAdvance_AutoAllocates_FromOverpaidReceipts()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db);
        var receipt = await SeedOverpaidReceiptAsync(db);
        var batch = await SeedAdvanceBatchAsync(db);

        var user = new TestCurrentUser(
            ["Accountant"],
            [AppPermissions.ImportCommitAdvance]);
        var audit = new AuditService(db, user);
        var service = new ImportCommitService(db, user, audit);

        var result = await service.CommitAsync(batch.Id, new ImportCommitRequest(null), CancellationToken.None);

        Assert.Equal(1, result.InsertedAdvances);

        var advance = await db.Advances.AsNoTracking().FirstAsync(a => a.SourceBatchId == batch.Id);
        var updatedReceipt = await db.Receipts.AsNoTracking().FirstAsync(r => r.Id == receipt.Id);
        var allocations = await db.ReceiptAllocations.AsNoTracking()
            .Where(a => a.AdvanceId == advance.Id)
            .ToListAsync();

        Assert.Single(allocations);
        Assert.Equal(500_000m, allocations[0].Amount);
        Assert.Equal("PAID", advance.Status);
        Assert.Equal(0m, advance.OutstandingAmount);
        Assert.Equal(100_000m, updatedReceipt.UnallocatedAmount);
        Assert.Equal("PARTIAL", updatedReceipt.AllocationStatus);
    }

    [Fact]
    public async Task CommitAdvance_SkipsReceipts_WhenAutoAllocateDisabled()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db);
        var receipt = await SeedOverpaidReceiptAsync(db, autoAllocateEnabled: false);
        var batch = await SeedAdvanceBatchAsync(db);

        var user = new TestCurrentUser(
            ["Accountant"],
            [AppPermissions.ImportCommitAdvance]);
        var audit = new AuditService(db, user);
        var service = new ImportCommitService(db, user, audit);

        var result = await service.CommitAsync(batch.Id, new ImportCommitRequest(null), CancellationToken.None);

        Assert.Equal(1, result.InsertedAdvances);

        var advance = await db.Advances.AsNoTracking().FirstAsync(a => a.SourceBatchId == batch.Id);
        var updatedReceipt = await db.Receipts.AsNoTracking().FirstAsync(r => r.Id == receipt.Id);
        var allocations = await db.ReceiptAllocations.AsNoTracking()
            .Where(a => a.AdvanceId == advance.Id)
            .ToListAsync();

        Assert.Empty(allocations);
        Assert.Equal("APPROVED", advance.Status);
        Assert.Equal(500_000m, advance.OutstandingAmount);
        Assert.Equal(600_000m, updatedReceipt.UnallocatedAmount);
        Assert.Equal("PARTIAL", updatedReceipt.AllocationStatus);
    }

    [Fact]
    public async Task CommitAdvance_CreatesMissingCustomer_BeforeInsertAdvance()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db);
        var batch = await SeedAdvanceBatchAsync(
            db,
            customerTaxCode: "CUSTNEW",
            customerName: "Customer New");

        var user = new TestCurrentUser(
            ["Accountant"],
            [AppPermissions.ImportCommitAdvance]);
        var audit = new AuditService(db, user);
        var service = new ImportCommitService(db, user, audit);

        var result = await service.CommitAsync(batch.Id, new ImportCommitRequest(null), CancellationToken.None);

        Assert.Equal(1, result.InsertedAdvances);

        var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.TaxCode == "CUSTNEW");
        var advance = await db.Advances.AsNoTracking().SingleAsync(a => a.SourceBatchId == batch.Id);

        Assert.Equal("Customer New", customer.Name);
        Assert.Equal(500_000m, customer.CurrentBalance);
        Assert.Equal("CUSTNEW", advance.CustomerTaxCode);
        Assert.Equal("APPROVED", advance.Status);
        Assert.Equal(500_000m, advance.OutstandingAmount);
    }

    [Fact]
    public async Task CommitAdvance_Skips_Duplicate_Document_Number_Already_In_System()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db);
        await SeedExistingAdvanceAsync(db, "SELLER01", "CUST01", "TH-NEW");
        var batch = await SeedAdvanceBatchAsync(db);

        var user = new TestCurrentUser(
            ["Accountant"],
            [AppPermissions.ImportCommitAdvance]);
        var audit = new AuditService(db, user);
        var service = new ImportCommitService(db, user, audit);

        var result = await service.CommitAsync(batch.Id, new ImportCommitRequest(null), CancellationToken.None);

        Assert.Equal(0, result.InsertedAdvances);
        Assert.Equal(1, result.TotalEligibleRows);
        Assert.Equal(0, result.CommittedRows);
        Assert.Equal(1, result.SkippedRows);
        Assert.Equal(1, await db.Advances.AsNoTracking().CountAsync(a => a.AdvanceNo == "TH-NEW"));
    }

    [Fact]
    public async Task Model_MapsCustomerForeignKeys_ForImportedDocuments()
    {
        await using var db = _fixture.CreateContext();

        AssertCustomerForeignKey<Invoice>(db, nameof(Invoice.CustomerTaxCode));
        AssertCustomerForeignKey<Advance>(db, nameof(Advance.CustomerTaxCode));
        AssertCustomerForeignKey<Receipt>(db, nameof(Receipt.CustomerTaxCode));
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.audit_logs, " +
            "congno.receipt_allocations, " +
            "congno.receipts, " +
            "congno.advances, " +
            "congno.import_staging_rows, " +
            "congno.import_batches, " +
            "congno.customers, " +
            "congno.sellers " +
            "RESTART IDENTITY CASCADE;");
    }

    private static async Task SeedSellerAsync(ConGNoDbContext db)
    {
        db.Sellers.Add(new Seller
        {
            SellerTaxCode = "SELLER01",
            Name = "Seller 01",
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private static async Task<Receipt> SeedOverpaidReceiptAsync(ConGNoDbContext db, bool autoAllocateEnabled = true)
    {
        var customer = new Customer
        {
            TaxCode = "CUST01",
            Name = "Customer 01",
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };
        db.Customers.Add(customer);

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = "SELLER01",
            CustomerTaxCode = customer.TaxCode,
            ReceiptNo = "PT-OVERPAY",
            ReceiptDate = new DateOnly(2026, 1, 20),
            Amount = 600_000m,
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationStatus = "PARTIAL",
            AllocationPriority = "ISSUE_DATE",
            AutoAllocateEnabled = autoAllocateEnabled,
            UnallocatedAmount = 600_000m,
            Status = "APPROVED",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Receipts.Add(receipt);
        await db.SaveChangesAsync();

        return receipt;
    }

    private static async Task<ImportBatch> SeedAdvanceBatchAsync(
        ConGNoDbContext db,
        string customerTaxCode = "CUST01",
        string customerName = "Customer 01")
    {
        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            Type = "ADVANCE",
            Source = "UPLOAD",
            Status = "STAGING",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ImportBatches.Add(batch);

        var raw = new Dictionary<string, object?>
        {
            ["seller_tax_code"] = "SELLER01",
            ["customer_tax_code"] = customerTaxCode,
            ["customer_name"] = customerName,
            ["advance_no"] = "TH-NEW",
            ["advance_date"] = "2026-02-01",
            ["amount"] = 500_000m,
            ["description"] = "Test advance"
        };

        db.ImportStagingRows.Add(new ImportStagingRow
        {
            Id = Guid.NewGuid(),
            BatchId = batch.Id,
            RowNo = 1,
            RawData = JsonSerializer.Serialize(raw),
            ValidationStatus = ImportStagingHelpers.StatusOk,
            ValidationMessages = "[]",
            ActionSuggestion = "INSERT",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        return batch;
    }

    private static async Task SeedExistingAdvanceAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        string advanceNo)
    {
        db.Customers.Add(new Customer
        {
            TaxCode = customerTaxCode,
            Name = "Customer 01",
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        db.Advances.Add(new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            AdvanceNo = advanceNo,
            AdvanceDate = new DateOnly(2026, 1, 1),
            Amount = 500_000m,
            OutstandingAmount = 500_000m,
            Description = "Existing advance",
            Status = "APPROVED",
            ApprovedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(IReadOnlyList<string> roles, IReadOnlyList<string> permissions)
        {
            Roles = roles;
            Permissions = permissions;
        }

        public Guid? UserId => Guid.Parse("44444444-4444-4444-4444-444444444444");
        public string? Username => "tester";
        public IReadOnlyList<string> Roles { get; }
        public IReadOnlyList<string> Permissions { get; }
        public string? IpAddress => "127.0.0.1";
    }

    private static void AssertCustomerForeignKey<TEntity>(
        ConGNoDbContext db,
        string foreignKeyPropertyName)
        where TEntity : class
    {
        var entityType = db.Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entityType);

        var foreignKey = entityType!.GetForeignKeys()
            .SingleOrDefault(fk =>
                fk.PrincipalEntityType.ClrType == typeof(Customer) &&
                fk.Properties.Select(p => p.Name).SequenceEqual(new[] { foreignKeyPropertyName }) &&
                fk.PrincipalKey.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Customer.TaxCode) }));

        Assert.NotNull(foreignKey);
    }
}
