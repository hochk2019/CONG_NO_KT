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
public class ImportCancelTests
{
    private readonly TestDatabaseFixture _fixture;

    public ImportCancelTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Cancel_Removes_Staging_Rows_And_Marks_Batch()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var batch = await SeedInvoiceBatchAsync(db);
        var user = new TestCurrentUser(new[] { "Accountant" });
        var audit = new AuditService(db, user);
        var service = new ImportCancelService(db, user, audit);

        var result = await service.CancelAsync(
            batch.Id,
            new ImportCancelRequest("upload sai"),
            CancellationToken.None);

        Assert.Equal(1, result.DeletedRows);

        var refreshed = await db.ImportBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        Assert.Equal("CANCELLED", refreshed.Status);
        Assert.NotNull(refreshed.CancelledAt);
        Assert.Equal("upload sai", refreshed.CancelReason);

        var rowsLeft = await db.ImportStagingRows.CountAsync(r => r.BatchId == batch.Id);
        Assert.Equal(0, rowsLeft);

        var log = await db.AuditLogs.FirstOrDefaultAsync(l => l.Action == "IMPORT_CANCEL");
        Assert.NotNull(log);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.audit_logs, " +
            "congno.import_staging_rows, " +
            "congno.import_batches, " +
            "congno.invoices, " +
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

        public Guid? UserId => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string? Username => "test";
        public IReadOnlyList<string> Roles { get; }
        public string? IpAddress => "127.0.0.1";
    }
}
