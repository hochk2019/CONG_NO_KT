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
public class ImportCommitReceiptTests
{
    private readonly TestDatabaseFixture _fixture;

    public ImportCommitReceiptTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CommitReceipt_CreatesMissingCustomer_BeforeInsertReceipt()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db);
        var batch = await SeedReceiptBatchAsync(
            db,
            customerTaxCode: "CUSTNEW",
            customerName: "Customer New");

        var user = new TestCurrentUser(
            ["Accountant"],
            [AppPermissions.ImportCommitReceipt]);
        var audit = new AuditService(db, user);
        var service = new ImportCommitService(db, user, audit);

        var result = await service.CommitAsync(batch.Id, new ImportCommitRequest(null), CancellationToken.None);

        Assert.Equal(1, result.InsertedReceipts);

        var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.TaxCode == "CUSTNEW");
        var receipt = await db.Receipts.AsNoTracking().SingleAsync(r => r.SourceBatchId == batch.Id);

        Assert.Equal("Customer New", customer.Name);
        Assert.Equal(0m, customer.CurrentBalance);
        Assert.Equal("CUSTNEW", receipt.CustomerTaxCode);
        Assert.Equal("DRAFT", receipt.Status);
        Assert.Equal("FIFO", receipt.AllocationMode);
        Assert.Equal(0m, receipt.UnallocatedAmount);
    }

    [Fact]
    public async Task CommitReceipt_AutoApprove_SetsUnallocatedAmountAndAllocationStatus()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db);
        var batch = await SeedReceiptBatchAsync(
            db,
            customerTaxCode: "CUSTNEW",
            customerName: "Customer New");

        var user = new TestCurrentUser(
            ["Accountant"],
            [AppPermissions.ImportCommitReceipt]);
        var audit = new AuditService(db, user);
        var service = new ImportCommitService(db, user, audit);

        var result = await service.CommitAsync(batch.Id, new ImportCommitRequest(null, AutoApprove: true), CancellationToken.None);

        Assert.Equal(1, result.InsertedReceipts);

        var receipt = await db.Receipts.AsNoTracking().SingleAsync(r => r.SourceBatchId == batch.Id);

        Assert.Equal("APPROVED", receipt.Status);
        Assert.True(receipt.AutoAllocateEnabled);
        Assert.Equal(500_000m, receipt.Amount);
        Assert.Equal(500_000m, receipt.UnallocatedAmount); // Should match amount because no targets exist
        Assert.Equal("UNALLOCATED", receipt.AllocationStatus);
    }

    [Fact]
    public async Task CommitReceipt_AutoApprove_FifoAllocates_ToExistingInvoice_InSameCommit()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db);
        await SeedCustomerAsync(db, "CUST01", "Customer 01", 500_000m);
        var invoiceId = await SeedInvoiceAsync(db, "SELLER01", "CUST01", "INV-001", 500_000m);
        var batch = await SeedReceiptBatchAsync(db);

        var user = new TestCurrentUser(
            ["Accountant"],
            [AppPermissions.ImportCommitReceipt]);
        var audit = new AuditService(db, user);
        var service = new ImportCommitService(db, user, audit);

        var result = await service.CommitAsync(batch.Id, new ImportCommitRequest(null, AutoApprove: true), CancellationToken.None);

        Assert.Equal(1, result.InsertedReceipts);

        var receipt = await db.Receipts.AsNoTracking().SingleAsync(r => r.SourceBatchId == batch.Id);
        var invoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
        var allocation = await db.ReceiptAllocations.AsNoTracking().SingleAsync(a => a.ReceiptId == receipt.Id);
        var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.TaxCode == "CUST01");

        Assert.Equal("ALLOCATED", receipt.AllocationStatus);
        Assert.Equal(0m, receipt.UnallocatedAmount);
        Assert.Equal("PAID", invoice.Status);
        Assert.Equal(0m, invoice.OutstandingAmount);
        Assert.Equal(invoiceId, allocation.InvoiceId);
        Assert.Equal(500_000m, allocation.Amount);
        Assert.Equal(0m, customer.CurrentBalance);
    }

    [Fact]
    public async Task CommitReceipt_Skips_Duplicate_Document_Number_Already_In_System()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db);
        await SeedExistingReceiptAsync(db, "SELLER01", "CUST01", "PT-NEW");
        var batch = await SeedReceiptBatchAsync(db);

        var user = new TestCurrentUser(
            ["Accountant"],
            [AppPermissions.ImportCommitReceipt]);
        var audit = new AuditService(db, user);
        var service = new ImportCommitService(db, user, audit);

        var result = await service.CommitAsync(batch.Id, new ImportCommitRequest(null), CancellationToken.None);

        Assert.Equal(0, result.InsertedReceipts);
        Assert.Equal(1, result.TotalEligibleRows);
        Assert.Equal(0, result.CommittedRows);
        Assert.Equal(1, result.SkippedRows);
        Assert.Equal(1, await db.Receipts.AsNoTracking().CountAsync(r => r.ReceiptNo == "PT-NEW"));
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.audit_logs, " +
            "congno.receipt_allocations, " +
            "congno.receipts, " +
            "congno.invoices, " +
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

    private static async Task SeedCustomerAsync(
        ConGNoDbContext db,
        string customerTaxCode,
        string name,
        decimal currentBalance)
    {
        db.Customers.Add(new Customer
        {
            TaxCode = customerTaxCode,
            Name = name,
            Status = "ACTIVE",
            CurrentBalance = currentBalance,
            PaymentTermsDays = 30,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedInvoiceAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        string invoiceNo,
        decimal amount)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            InvoiceNo = invoiceNo,
            IssueDate = new DateOnly(2026, 1, 10),
            RevenueExclVat = amount,
            VatAmount = 0,
            TotalAmount = amount,
            OutstandingAmount = amount,
            InvoiceType = "NORMAL",
            Status = "OPEN",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    private static async Task<ImportBatch> SeedReceiptBatchAsync(
        ConGNoDbContext db,
        string customerTaxCode = "CUST01",
        string customerName = "Customer 01")
    {
        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            Type = "RECEIPT",
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
            ["receipt_no"] = "PT-NEW",
            ["receipt_date"] = "2026-02-01",
            ["amount"] = 500_000m,
            ["method"] = "BANK",
            ["description"] = "Test receipt"
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

    private static async Task SeedExistingReceiptAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        string receiptNo)
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

        db.Receipts.Add(new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            ReceiptNo = receiptNo,
            ReceiptDate = new DateOnly(2026, 2, 1),
            AppliedPeriodStart = new DateOnly(2026, 2, 1),
            Amount = 500_000m,
            Method = "BANK",
            Description = "Existing receipt",
            AllocationMode = "FIFO",
            UnallocatedAmount = 0m,
            Status = "DRAFT",
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

        public Guid? UserId => Guid.Parse("66666666-6666-6666-6666-666666666666");
        public string? Username => "tester";
        public IReadOnlyList<string> Roles { get; }
        public IReadOnlyList<string> Permissions { get; }
        public string? IpAddress => "127.0.0.1";
    }
}
