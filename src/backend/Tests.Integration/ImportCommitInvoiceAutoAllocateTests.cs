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
public class ImportCommitInvoiceAutoAllocateTests
{
    private readonly TestDatabaseFixture _fixture;

    public ImportCommitInvoiceAutoAllocateTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CommitInvoice_AutoAllocates_FromOverpaidReceipts()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db);
        var receipt = await SeedOverpaidReceiptAsync(db);
        var batch = await SeedInvoiceBatchAsync(db);

        var user = new TestCurrentUser(new[] { "Accountant" });
        var audit = new AuditService(db, user);
        var service = new ImportCommitService(db, user, audit);

        var result = await service.CommitAsync(batch.Id, new ImportCommitRequest(null), CancellationToken.None);

        Assert.Equal(1, result.InsertedInvoices);

        var invoice = await db.Invoices.AsNoTracking().FirstAsync(i => i.SourceBatchId == batch.Id);
        var updatedReceipt = await db.Receipts.AsNoTracking().FirstAsync(r => r.Id == receipt.Id);
        var allocations = await db.ReceiptAllocations.AsNoTracking()
            .Where(a => a.InvoiceId == invoice.Id)
            .ToListAsync();

        Assert.Single(allocations);
        Assert.Equal(500_000m, allocations[0].Amount);
        Assert.Equal("PAID", invoice.Status);
        Assert.Equal(0m, invoice.OutstandingAmount);
        Assert.Equal(100_000m, updatedReceipt.UnallocatedAmount);
        Assert.Equal("PARTIAL", updatedReceipt.AllocationStatus);
    }

    [Fact]
    public async Task CommitInvoice_SkipsReceipts_WhenAutoAllocateDisabled()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db);
        var receipt = await SeedOverpaidReceiptAsync(db, autoAllocateEnabled: false);
        var batch = await SeedInvoiceBatchAsync(db);

        var user = new TestCurrentUser(new[] { "Accountant" });
        var audit = new AuditService(db, user);
        var service = new ImportCommitService(db, user, audit);

        var result = await service.CommitAsync(batch.Id, new ImportCommitRequest(null), CancellationToken.None);

        Assert.Equal(1, result.InsertedInvoices);

        var invoice = await db.Invoices.AsNoTracking().FirstAsync(i => i.SourceBatchId == batch.Id);
        var updatedReceipt = await db.Receipts.AsNoTracking().FirstAsync(r => r.Id == receipt.Id);
        var allocations = await db.ReceiptAllocations.AsNoTracking()
            .Where(a => a.InvoiceId == invoice.Id)
            .ToListAsync();

        Assert.Empty(allocations);
        Assert.Equal("OPEN", invoice.Status);
        Assert.Equal(500_000m, invoice.OutstandingAmount);
        Assert.Equal(600_000m, updatedReceipt.UnallocatedAmount);
        Assert.Equal("PARTIAL", updatedReceipt.AllocationStatus);
    }

    [Fact]
    public async Task CommitInvoice_CreatesMissingCustomer_BeforeInsertInvoice()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db);
        var batch = await SeedInvoiceBatchAsync(
            db,
            customerTaxCode: "CUSTNEW",
            customerName: "Customer New");

        var user = new TestCurrentUser(new[] { "Accountant" });
        var audit = new AuditService(db, user);
        var service = new ImportCommitService(db, user, audit);

        var result = await service.CommitAsync(batch.Id, new ImportCommitRequest(null), CancellationToken.None);

        Assert.Equal(1, result.InsertedInvoices);

        var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.TaxCode == "CUSTNEW");
        var invoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.SourceBatchId == batch.Id);

        Assert.Equal("Customer New", customer.Name);
        Assert.Equal(500_000m, customer.CurrentBalance);
        Assert.Equal("CUSTNEW", invoice.CustomerTaxCode);
        Assert.Equal("OPEN", invoice.Status);
        Assert.Equal(500_000m, invoice.OutstandingAmount);
    }

    [Fact]
    public async Task CommitInvoice_ReductionRow_UsesMatchingTaxCode_And_Reduces_OpenInvoice()
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
        var batch = await SeedInvoiceBatchAsync(
            db,
            customerTaxCode: "0310226744-003",
            customerName: "Customer Root",
            revenueExclVat: -200_000m,
            vatAmount: -20_000m,
            invoiceNo: "ADJ001",
            note: "Hóa đơn điều chỉnh giảm",
            extraRaw: new Dictionary<string, object?>
            {
                ["customer_tax_code_matching"] = "0310226744",
                ["invoice_type"] = "ADJUSTMENT_REDUCTION"
            });

        var user = new TestCurrentUser(new[] { "Accountant" });
        var audit = new AuditService(db, user);
        var service = new ImportCommitService(db, user, audit);

        var result = await service.CommitAsync(batch.Id, new ImportCommitRequest(null), CancellationToken.None);

        Assert.Equal(1, result.InsertedInvoices);

        var adjustmentInvoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.SourceBatchId == batch.Id);
        var updatedOriginal = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == originalInvoice.Id);
        var rootCustomer = await db.Customers.AsNoTracking().SingleAsync(c => c.TaxCode == "0310226744");

        Assert.Equal("0310226744", adjustmentInvoice.CustomerTaxCode);
        Assert.Equal("ADJUSTMENT_REDUCTION", adjustmentInvoice.InvoiceType);
        Assert.Equal(-220_000m, adjustmentInvoice.TotalAmount);
        Assert.Equal(0m, adjustmentInvoice.OutstandingAmount);
        Assert.Equal("PAID", adjustmentInvoice.Status);

        Assert.Equal(280_000m, updatedOriginal.OutstandingAmount);
        Assert.Equal("PARTIAL", updatedOriginal.Status);
        Assert.Equal(280_000m, rootCustomer.CurrentBalance);
        Assert.False(await db.Customers.AsNoTracking().AnyAsync(c => c.TaxCode == "0310226744-003"));
    }

    [Fact]
    public async Task CommitInvoice_ReductionRow_Prefers_Unique_Direct_Match_Before_Fifo()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db);
        var fifoCandidate = await SeedOpenInvoiceAsync(
            db,
            customerTaxCode: "0310226744",
            customerName: "Customer Root",
            totalAmount: 300_000m,
            invoiceNo: "INV-FIFO-OLDER",
            issueDate: new DateOnly(2026, 1, 5));
        var directMatch = await SeedOpenInvoiceAsync(
            db,
            customerTaxCode: "0310226744",
            customerName: "Customer Root",
            totalAmount: 220_000m,
            invoiceNo: "INV-DIRECT-EXACT",
            issueDate: new DateOnly(2026, 1, 20));
        var batch = await SeedInvoiceBatchAsync(
            db,
            customerTaxCode: "0310226744-003",
            customerName: "Customer Root",
            revenueExclVat: -200_000m,
            vatAmount: -20_000m,
            invoiceNo: "ADJ-DIRECT-01",
            note: "Hóa đơn điều chỉnh giảm",
            extraRaw: new Dictionary<string, object?>
            {
                ["customer_tax_code_matching"] = "0310226744",
                ["invoice_type"] = "ADJUSTMENT_REDUCTION"
            });

        var user = new TestCurrentUser(new[] { "Accountant" });
        var audit = new AuditService(db, user);
        var service = new ImportCommitService(db, user, audit);

        var result = await service.CommitAsync(batch.Id, new ImportCommitRequest(null), CancellationToken.None);

        Assert.Equal(1, result.InsertedInvoices);

        var updatedFifoCandidate = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == fifoCandidate.Id);
        var updatedDirectMatch = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == directMatch.Id);
        var adjustmentInvoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.SourceBatchId == batch.Id);
        var rootCustomer = await db.Customers.AsNoTracking().SingleAsync(c => c.TaxCode == "0310226744");

        Assert.Equal("0310226744", adjustmentInvoice.CustomerTaxCode);
        Assert.Equal("ADJUSTMENT_REDUCTION", adjustmentInvoice.InvoiceType);
        Assert.Equal(-220_000m, adjustmentInvoice.TotalAmount);
        Assert.Equal(0m, adjustmentInvoice.OutstandingAmount);
        Assert.Equal("PAID", adjustmentInvoice.Status);

        Assert.Equal(300_000m, updatedFifoCandidate.OutstandingAmount);
        Assert.Equal("OPEN", updatedFifoCandidate.Status);
        Assert.Equal(0m, updatedDirectMatch.OutstandingAmount);
        Assert.Equal("PAID", updatedDirectMatch.Status);
        Assert.Equal(300_000m, rootCustomer.CurrentBalance);
    }

    [Fact]
    public async Task CommitInvoice_ReductionRow_Falls_Back_To_Fifo_When_Direct_Match_Is_Ambiguous()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db);
        var olderInvoice = await SeedOpenInvoiceAsync(
            db,
            customerTaxCode: "0310226744",
            customerName: "Customer Root",
            totalAmount: 220_000m,
            invoiceNo: "INV-OLDER-EXACT",
            issueDate: new DateOnly(2026, 1, 5));
        var newerInvoice = await SeedOpenInvoiceAsync(
            db,
            customerTaxCode: "0310226744",
            customerName: "Customer Root",
            totalAmount: 220_000m,
            invoiceNo: "INV-NEWER-EXACT",
            issueDate: new DateOnly(2026, 1, 20));
        var batch = await SeedInvoiceBatchAsync(
            db,
            customerTaxCode: "0310226744-003",
            customerName: "Customer Root",
            revenueExclVat: -200_000m,
            vatAmount: -20_000m,
            invoiceNo: "ADJ-FIFO-01",
            note: "Hóa đơn điều chỉnh giảm",
            extraRaw: new Dictionary<string, object?>
            {
                ["customer_tax_code_matching"] = "0310226744",
                ["invoice_type"] = "ADJUSTMENT_REDUCTION"
            });

        var user = new TestCurrentUser(new[] { "Accountant" });
        var audit = new AuditService(db, user);
        var service = new ImportCommitService(db, user, audit);

        var result = await service.CommitAsync(batch.Id, new ImportCommitRequest(null), CancellationToken.None);

        Assert.Equal(1, result.InsertedInvoices);

        var updatedOlderInvoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == olderInvoice.Id);
        var updatedNewerInvoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == newerInvoice.Id);
        var adjustmentInvoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.SourceBatchId == batch.Id);
        var rootCustomer = await db.Customers.AsNoTracking().SingleAsync(c => c.TaxCode == "0310226744");

        Assert.Equal("0310226744", adjustmentInvoice.CustomerTaxCode);
        Assert.Equal("ADJUSTMENT_REDUCTION", adjustmentInvoice.InvoiceType);
        Assert.Equal(-220_000m, adjustmentInvoice.TotalAmount);
        Assert.Equal(0m, adjustmentInvoice.OutstandingAmount);
        Assert.Equal("PAID", adjustmentInvoice.Status);

        Assert.Equal(0m, updatedOlderInvoice.OutstandingAmount);
        Assert.Equal("PAID", updatedOlderInvoice.Status);
        Assert.Equal(220_000m, updatedNewerInvoice.OutstandingAmount);
        Assert.Equal("OPEN", updatedNewerInvoice.Status);
        Assert.Equal(220_000m, rootCustomer.CurrentBalance);
    }

    [Fact]
    public async Task CommitInvoice_ReductionResidual_Becomes_CarryForwardCredit_OnRootCustomer()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db);
        var originalInvoice = await SeedOpenInvoiceAsync(
            db,
            customerTaxCode: "0310226744",
            customerName: "Customer Root",
            totalAmount: 100_000m,
            invoiceNo: "INV-OPEN-RESIDUAL");
        var batch = await SeedInvoiceBatchAsync(
            db,
            customerTaxCode: "0310226744-003",
            customerName: "Customer Root",
            revenueExclVat: -200_000m,
            vatAmount: -20_000m,
            invoiceNo: "ADJ-RESIDUAL-01",
            note: "Hóa đơn điều chỉnh giảm",
            extraRaw: new Dictionary<string, object?>
            {
                ["customer_tax_code_matching"] = "0310226744",
                ["invoice_type"] = "ADJUSTMENT_REDUCTION"
            });

        var user = new TestCurrentUser(new[] { "Accountant" });
        var audit = new AuditService(db, user);
        var service = new ImportCommitService(db, user, audit);

        var result = await service.CommitAsync(batch.Id, new ImportCommitRequest(null), CancellationToken.None);

        Assert.Equal(1, result.InsertedInvoices);

        var adjustmentInvoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.SourceBatchId == batch.Id);
        var updatedOriginal = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == originalInvoice.Id);
        var rootCustomer = await db.Customers.AsNoTracking().SingleAsync(c => c.TaxCode == "0310226744");

        Assert.Equal("0310226744", adjustmentInvoice.CustomerTaxCode);
        Assert.Equal("ADJUSTMENT_REDUCTION", adjustmentInvoice.InvoiceType);
        Assert.Equal(-220_000m, adjustmentInvoice.TotalAmount);
        Assert.Equal(0m, adjustmentInvoice.OutstandingAmount);
        Assert.Equal("PAID", adjustmentInvoice.Status);

        Assert.Equal(0m, updatedOriginal.OutstandingAmount);
        Assert.Equal("PAID", updatedOriginal.Status);
        Assert.Equal(-120_000m, rootCustomer.CurrentBalance);
        Assert.False(await db.Customers.AsNoTracking().AnyAsync(c => c.TaxCode == "0310226744-003"));
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

    private static async Task<ImportBatch> SeedInvoiceBatchAsync(
        ConGNoDbContext db,
        string customerTaxCode = "CUST01",
        string customerName = "Customer 01",
        decimal revenueExclVat = 400_000m,
        decimal vatAmount = 100_000m,
        string invoiceNo = "INV001",
        string? note = null,
        IReadOnlyDictionary<string, object?>? extraRaw = null)
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
            ["customer_tax_code"] = customerTaxCode,
            ["customer_name"] = customerName,
            ["invoice_template_code"] = "01GTKT",
            ["invoice_series"] = "AA/23E",
            ["invoice_no"] = invoiceNo,
            ["issue_date"] = "2026-02-01",
            ["revenue_excl_vat"] = revenueExclVat,
            ["vat_amount"] = vatAmount,
            ["total_amount"] = revenueExclVat + vatAmount,
            ["note"] = note
        };

        if (extraRaw is not null)
        {
            foreach (var pair in extraRaw)
            {
                raw[pair.Key] = pair.Value;
            }
        }

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
        string invoiceNo,
        DateOnly? issueDate = null)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.TaxCode == customerTaxCode);
        if (customer is null)
        {
            customer = new Customer
            {
                TaxCode = customerTaxCode,
                Name = customerName,
                Status = "ACTIVE",
                CurrentBalance = 0m,
                PaymentTermsDays = 30,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Version = 0
            };
            db.Customers.Add(customer);
        }

        customer.CurrentBalance += totalAmount;
        customer.UpdatedAt = DateTimeOffset.UtcNow;
        customer.Version += 1;

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = "SELLER01",
            CustomerTaxCode = customerTaxCode,
            InvoiceTemplateCode = "01GTKT",
            InvoiceSeries = "AA/23E",
            InvoiceNo = invoiceNo,
            IssueDate = issueDate ?? new DateOnly(2026, 1, 10),
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
        public TestCurrentUser(IReadOnlyList<string> roles)
        {
            Roles = roles;
        }

        public Guid? UserId => Guid.Parse("55555555-5555-5555-5555-555555555555");
        public string? Username => "tester";
        public IReadOnlyList<string> Roles { get; }
        public string? IpAddress => "127.0.0.1";
    }
}
