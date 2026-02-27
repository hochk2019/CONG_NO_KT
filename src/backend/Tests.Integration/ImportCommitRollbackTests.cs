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
public class ImportCommitRollbackTests
{
    private readonly TestDatabaseFixture _fixture;

    public ImportCommitRollbackTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Commit_Then_Rollback_Updates_Status_And_Deletes_Data()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var batch = await SeedInvoiceBatchAsync(db);
        var user = new TestCurrentUser(new[] { "Accountant" });
        var audit = new AuditService(db, user);
        var commitService = new ImportCommitService(db, user, audit);

        var commitResult = await commitService.CommitAsync(
            batch.Id,
            new ImportCommitRequest(null),
            CancellationToken.None);

        Assert.Equal(1, commitResult.InsertedInvoices);
        Assert.Equal(1, commitResult.TotalEligibleRows);
        Assert.Equal(1, commitResult.CommittedRows);
        Assert.Equal(0, commitResult.SkippedRows);
        Assert.NotNull(commitResult.ProgressSteps);
        Assert.NotEmpty(commitResult.ProgressSteps!);

        var committed = await db.ImportBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        Assert.Equal("COMMITTED", committed.Status);

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.TaxCode == "CUST01");
        Assert.NotNull(customer);
        Assert.Equal(110m, customer!.CurrentBalance);

        var rollbackService = new ImportRollbackService(db, user, audit);
        var rollbackResult = await rollbackService.RollbackAsync(
            batch.Id,
            new ImportRollbackRequest(),
            CancellationToken.None);

        Assert.Equal(1, rollbackResult.RolledBackInvoices);

        var rolled = await db.ImportBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        Assert.Equal("ROLLED_BACK", rolled.Status);

        var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.SourceBatchId == batch.Id);
        Assert.NotNull(invoice);
        Assert.NotNull(invoice!.DeletedAt);

        var customerAfter = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.TaxCode == "CUST01");
        Assert.NotNull(customerAfter);
        Assert.Equal(0m, customerAfter!.CurrentBalance);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.audit_logs, " +
            "congno.receipt_allocations, " +
            "congno.receipts, " +
            "congno.advances, " +
            "congno.invoices, " +
            "congno.period_locks, " +
            "congno.import_staging_rows, " +
            "congno.import_batches, " +
            "congno.customers, " +
            "congno.sellers " +
            "RESTART IDENTITY CASCADE;");
    }

    private static async Task<ImportBatch> SeedInvoiceBatchAsync(ConGNoDbContext db)
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
        db.Sellers.Add(seller);

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            Type = "INVOICE",
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
            ["invoice_template_code"] = "01GTKT",
            ["invoice_series"] = "AA/23E",
            ["invoice_no"] = "INV001",
            ["issue_date"] = "2025-01-15",
            ["revenue_excl_vat"] = 100m,
            ["vat_amount"] = 10m,
            ["total_amount"] = 110m
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

        public Guid? UserId => Guid.Parse("22222222-2222-2222-2222-222222222222");
        public string? Username => "tester";
        public IReadOnlyList<string> Roles { get; }
        public string? IpAddress => "127.0.0.1";
    }
}
