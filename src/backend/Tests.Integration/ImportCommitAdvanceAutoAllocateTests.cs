using System.Text.Json;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Imports;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
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

        var user = new TestCurrentUser(new[] { "Accountant" });
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

    private static async Task<Receipt> SeedOverpaidReceiptAsync(ConGNoDbContext db)
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

    private static async Task<ImportBatch> SeedAdvanceBatchAsync(ConGNoDbContext db)
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
            ["customer_tax_code"] = "CUST01",
            ["customer_name"] = "Customer 01",
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

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(IReadOnlyList<string> roles)
        {
            Roles = roles;
        }

        public Guid? UserId => Guid.Parse("44444444-4444-4444-4444-444444444444");
        public string? Username => "tester";
        public IReadOnlyList<string> Roles { get; }
        public string? IpAddress => "127.0.0.1";
    }
}
