using System.Text.Json;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Imports;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Security;
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
        var user = new TestCurrentUser(
            permissions:
            [
                AppPermissions.ImportCommitInvoice,
                AppPermissions.ImportRollback
            ]);
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

    [Fact]
    public async Task Commit_Then_Rollback_ReductionInvoice_Restores_Reduced_OpenInvoice()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db);
        var originalInvoice = await SeedOpenInvoiceAsync(
            db,
            customerTaxCode: "0310226744",
            customerName: "Customer Root",
            totalAmount: 500_000m,
            invoiceNo: "INV-OPEN-01");
        var batch = await SeedReductionBatchAsync(db);

        var user = new TestCurrentUser(
            permissions:
            [
                AppPermissions.ImportCommitInvoice,
                AppPermissions.ImportRollback
            ]);
        var audit = new AuditService(db, user);
        var commitService = new ImportCommitService(db, user, audit);

        var commitResult = await commitService.CommitAsync(
            batch.Id,
            new ImportCommitRequest(null),
            CancellationToken.None);

        Assert.Equal(1, commitResult.InsertedInvoices);

        var reducedInvoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == originalInvoice.Id);
        Assert.Equal(280_000m, reducedInvoice.OutstandingAmount);
        Assert.Equal("PARTIAL", reducedInvoice.Status);

        var rollbackService = new ImportRollbackService(db, user, audit);
        var rollbackResult = await rollbackService.RollbackAsync(
            batch.Id,
            new ImportRollbackRequest(),
            CancellationToken.None);

        Assert.Equal(1, rollbackResult.RolledBackInvoices);

        var adjustmentInvoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.SourceBatchId == batch.Id);
        var restoredInvoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == originalInvoice.Id);
        var rootCustomer = await db.Customers.AsNoTracking().SingleAsync(c => c.TaxCode == "0310226744");

        Assert.NotNull(adjustmentInvoice.DeletedAt);
        Assert.Equal(500_000m, restoredInvoice.OutstandingAmount);
        Assert.Equal("OPEN", restoredInvoice.Status);
        Assert.Equal(500_000m, rootCustomer.CurrentBalance);
    }

    [Fact]
    public async Task Commit_ThrowsUnauthorized_WhenUserLacksBatchTypePermission()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var batch = await SeedInvoiceBatchAsync(db);
        var user = new TestCurrentUser(permissions: [AppPermissions.ImportHistory]);
        var audit = new AuditService(db, user);
        var commitService = new ImportCommitService(db, user, audit);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            commitService.CommitAsync(
                batch.Id,
                new ImportCommitRequest(null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rollback_ThrowsUnauthorized_WhenUserLacksRollbackPermission()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var batch = await SeedInvoiceBatchAsync(db);
        var commitUser = new TestCurrentUser(permissions: [AppPermissions.ImportCommitInvoice]);
        var commitAudit = new AuditService(db, commitUser);
        var commitService = new ImportCommitService(db, commitUser, commitAudit);

        await commitService.CommitAsync(
            batch.Id,
            new ImportCommitRequest(null),
            CancellationToken.None);

        var rollbackUser = new TestCurrentUser(permissions: [AppPermissions.ImportHistory]);
        var rollbackAudit = new AuditService(db, rollbackUser);
        var rollbackService = new ImportRollbackService(db, rollbackUser, rollbackAudit);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            rollbackService.RollbackAsync(
                batch.Id,
                new ImportRollbackRequest(),
                CancellationToken.None));
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

    private static async Task<ImportBatch> SeedReductionBatchAsync(ConGNoDbContext db)
    {
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
            ["customer_tax_code"] = "0310226744-003",
            ["customer_tax_code_matching"] = "0310226744",
            ["customer_name"] = "Customer Root",
            ["invoice_template_code"] = "01GTKT",
            ["invoice_series"] = "AA/23E",
            ["invoice_no"] = "ADJ001",
            ["issue_date"] = "2025-01-15",
            ["revenue_excl_vat"] = -200_000m,
            ["vat_amount"] = -20_000m,
            ["total_amount"] = -220_000m,
            ["invoice_type"] = "ADJUSTMENT_REDUCTION",
            ["note"] = "Hóa đơn điều chỉnh giảm"
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

    private static async Task<Invoice> SeedOpenInvoiceAsync(
        ConGNoDbContext db,
        string customerTaxCode,
        string customerName,
        decimal totalAmount,
        string invoiceNo)
    {
        var customer = new Customer
        {
            TaxCode = customerTaxCode,
            Name = customerName,
            Status = "ACTIVE",
            CurrentBalance = totalAmount,
            PaymentTermsDays = 30,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };
        db.Customers.Add(customer);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = "SELLER01",
            CustomerTaxCode = customerTaxCode,
            InvoiceTemplateCode = "01GTKT",
            InvoiceSeries = "AA/23E",
            InvoiceNo = invoiceNo,
            IssueDate = new DateOnly(2025, 1, 10),
            RevenueExclVat = 400_000m,
            VatAmount = 100_000m,
            TotalAmount = totalAmount,
            OutstandingAmount = totalAmount,
            InvoiceType = "NORMAL",
            Status = "OPEN",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };
        db.Invoices.Add(invoice);

        await db.SaveChangesAsync();

        return invoice;
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(
            IReadOnlyList<string>? roles = null,
            IReadOnlyList<string>? permissions = null)
        {
            Roles = roles ?? Array.Empty<string>();
            Permissions = permissions ?? Array.Empty<string>();
        }

        public Guid? UserId => Guid.Parse("22222222-2222-2222-2222-222222222222");
        public string? Username => "tester";
        public IReadOnlyList<string> Roles { get; }
        public IReadOnlyList<string> Permissions { get; }
        public string? IpAddress => "127.0.0.1";
    }
}
