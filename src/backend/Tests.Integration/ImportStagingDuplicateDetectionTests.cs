using System.Text.Json;
using ClosedXML.Excel;
using CongNoGolden.Application.Imports;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class ImportStagingDuplicateDetectionTests
{
    private readonly TestDatabaseFixture _fixture;

    public ImportStagingDuplicateDetectionTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StageInvoice_ReductionRow_WithBranchTaxCode_Marks_Duplicate_Against_RootCustomerInvoice()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db, "2300328765");
        await SeedExistingReductionInvoiceAsync(db);

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            Type = "INVOICE",
            Source = "UPLOAD",
            Status = "STAGING",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();

        await using var stream = BuildReductionWorkbook();
        var service = new ImportStagingService(db);

        var result = await service.StageAsync(batch.Id, "INVOICE", stream, CancellationToken.None);

        Assert.Equal(1, result.TotalRows);

        var row = await db.ImportStagingRows.AsNoTracking().SingleAsync(r => r.BatchId == batch.Id);
        var messages = ReadMessages(row.ValidationMessages);

        Assert.Equal("SKIP", row.ActionSuggestion);
        Assert.Equal(ImportStagingHelpers.StatusWarn, row.ValidationStatus);
        Assert.Contains("DUP_IN_DB", messages);
    }

    [Fact]
    public async Task StageAdvance_Marks_Duplicate_Against_ExistingAdvanceNo()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db, "SELLER01");
        await SeedCustomerAsync(db, "CUST01", "Customer 01");
        await SeedExistingAdvanceAsync(db, "SELLER01", "CUST01", "ADV-001");

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            Type = "ADVANCE",
            Source = "UPLOAD",
            Status = "STAGING",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();

        await using var stream = BuildAdvanceWorkbook("SELLER01", "CUST01", "ADV-001");
        var service = new ImportStagingService(db);

        var result = await service.StageAsync(batch.Id, "ADVANCE", stream, CancellationToken.None);

        Assert.Equal(1, result.TotalRows);

        var row = await db.ImportStagingRows.AsNoTracking().SingleAsync(r => r.BatchId == batch.Id);
        var messages = ReadMessages(row.ValidationMessages);

        Assert.Equal("SKIP", row.ActionSuggestion);
        Assert.Equal(ImportStagingHelpers.StatusWarn, row.ValidationStatus);
        Assert.Contains("DUP_IN_DB", messages);
    }

    [Fact]
    public async Task StageReceipt_Marks_Duplicate_Against_ExistingReceiptNo()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db, "SELLER01");
        await SeedCustomerAsync(db, "CUST01", "Customer 01");
        await SeedExistingReceiptAsync(db, "SELLER01", "CUST01", "PT-001");

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            Type = "RECEIPT",
            Source = "UPLOAD",
            Status = "STAGING",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();

        await using var stream = BuildReceiptWorkbook("SELLER01", "CUST01", "PT-001");
        var service = new ImportStagingService(db);

        var result = await service.StageAsync(batch.Id, "RECEIPT", stream, CancellationToken.None);

        Assert.Equal(1, result.TotalRows);

        var row = await db.ImportStagingRows.AsNoTracking().SingleAsync(r => r.BatchId == batch.Id);
        var messages = ReadMessages(row.ValidationMessages);

        Assert.Equal("SKIP", row.ActionSuggestion);
        Assert.Equal(ImportStagingHelpers.StatusWarn, row.ValidationStatus);
        Assert.Contains("DUP_IN_DB", messages);
    }

    [Fact]
    public async Task StageInvoice_SystemTemplateFile_Preserves_IssueDate()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db, "2300328765");
        await SeedCustomerAsync(db, "0101000002", "Cong ty TNHH Mau");

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            Type = "INVOICE",
            Source = "UPLOAD",
            Status = "STAGING",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();

        var templatePath = FindRepoFile("src", "frontend", "public", "templates", "invoice_template.xlsx");
        await using var stream = File.OpenRead(templatePath);
        var service = new ImportStagingService(db);

        var result = await service.StageAsync(batch.Id, "INVOICE", stream, CancellationToken.None);

        Assert.Equal(1, result.TotalRows);

        var row = await db.ImportStagingRows.AsNoTracking().SingleAsync(r => r.BatchId == batch.Id);
        var messages = ReadMessages(row.ValidationMessages);

        Assert.DoesNotContain("ISSUE_DATE_REQUIRED", messages);
        Assert.Equal("2026-01-15", ReadRawString(row.RawData, "issue_date"));
        Assert.Equal("Cong ty TNHH Mau", ReadRawString(row.RawData, "customer_name"));
    }

    [Fact]
    public async Task StageAdvance_SystemTemplateFile_Preserves_All_SnakeCase_Columns()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db, "2300328765");
        await SeedCustomerAsync(db, "0101000002", "Cong ty TNHH Mau");

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            Type = "ADVANCE",
            Source = "UPLOAD",
            Status = "STAGING",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();

        var templatePath = FindRepoFile("src", "frontend", "public", "templates", "advance_template.xlsx");
        await using var stream = File.OpenRead(templatePath);
        var service = new ImportStagingService(db);

        var result = await service.StageAsync(batch.Id, "ADVANCE", stream, CancellationToken.None);

        Assert.Equal(1, result.TotalRows);

        var row = await db.ImportStagingRows.AsNoTracking().SingleAsync(r => r.BatchId == batch.Id);
        var messages = ReadMessages(row.ValidationMessages);

        Assert.DoesNotContain("ADVANCE_DATE_REQUIRED", messages);
        Assert.Equal("2300328765", ReadRawString(row.RawData, "seller_tax_code"));
        Assert.Equal("0101000002", ReadRawString(row.RawData, "customer_tax_code"));
        Assert.Equal("TH-2026-0001", ReadRawString(row.RawData, "advance_no"));
        Assert.Equal("2026-01-10", ReadRawString(row.RawData, "advance_date"));
        Assert.Equal(1500000m, ReadRawDecimal(row.RawData, "amount"));
        Assert.Equal("Tra ho cuoc van chuyen", ReadRawString(row.RawData, "description"));
    }

    [Fact]
    public async Task StageReceipt_SystemTemplateFile_Preserves_All_SnakeCase_Columns()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedSellerAsync(db, "2300328765");
        await SeedCustomerAsync(db, "0101000002", "Cong ty TNHH Mau");

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            Type = "RECEIPT",
            Source = "UPLOAD",
            Status = "STAGING",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();

        var templatePath = FindRepoFile("src", "frontend", "public", "templates", "receipt_template.xlsx");
        await using var stream = File.OpenRead(templatePath);
        var service = new ImportStagingService(db);

        var result = await service.StageAsync(batch.Id, "RECEIPT", stream, CancellationToken.None);

        Assert.Equal(1, result.TotalRows);

        var row = await db.ImportStagingRows.AsNoTracking().SingleAsync(r => r.BatchId == batch.Id);
        var messages = ReadMessages(row.ValidationMessages);

        Assert.DoesNotContain("RECEIPT_DATE_REQUIRED", messages);
        Assert.DoesNotContain("APPLIED_PERIOD_REQUIRED", messages);
        Assert.Equal("2300328765", ReadRawString(row.RawData, "seller_tax_code"));
        Assert.Equal("0101000002", ReadRawString(row.RawData, "customer_tax_code"));
        Assert.Equal("PT-2026-0001", ReadRawString(row.RawData, "receipt_no"));
        Assert.Equal("2026-01-12", ReadRawString(row.RawData, "receipt_date"));
        Assert.Equal("2026-01-01", ReadRawString(row.RawData, "applied_period_start"));
        Assert.Equal(2500000m, ReadRawDecimal(row.RawData, "amount"));
        Assert.Equal("BANK", ReadRawString(row.RawData, "method"));
        Assert.Equal("Thu cong no thang 01/2026", ReadRawString(row.RawData, "description"));
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
            "congno.import_staging_rows, " +
            "congno.import_batches, " +
            "congno.customers, " +
            "congno.sellers " +
            "RESTART IDENTITY CASCADE;");
    }

    private static async Task SeedSellerAsync(ConGNoDbContext db, string sellerTaxCode)
    {
        db.Sellers.Add(new Seller
        {
            SellerTaxCode = sellerTaxCode,
            Name = "Seller Test",
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedCustomerAsync(ConGNoDbContext db, string taxCode, string name)
    {
        db.Customers.Add(new Customer
        {
            TaxCode = taxCode,
            Name = name,
            Status = "ACTIVE",
            CurrentBalance = 0m,
            PaymentTermsDays = 30,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedExistingReductionInvoiceAsync(ConGNoDbContext db)
    {
        await SeedCustomerAsync(
            db,
            "0310226744",
            "CHI NHÁNH CÔNG TY TNHH LX PANTOS VIỆT NAM TẠI HẢI PHÒNG");

        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = "2300328765",
            CustomerTaxCode = "0310226744",
            InvoiceTemplateCode = "1C25THK",
            InvoiceSeries = string.Empty,
            InvoiceNo = "2233",
            IssueDate = new DateOnly(2025, 7, 15),
            RevenueExclVat = -2_100_000m,
            VatAmount = -168_000m,
            TotalAmount = -2_268_000m,
            OutstandingAmount = 0m,
            Note = "Hóa đơn điều chỉnh giảm",
            InvoiceType = "ADJUSTMENT_REDUCTION",
            Status = "PAID",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedExistingAdvanceAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        string advanceNo)
    {
        db.Advances.Add(new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            AdvanceNo = advanceNo,
            AdvanceDate = new DateOnly(2026, 1, 1),
            Amount = 100_000m,
            OutstandingAmount = 100_000m,
            Description = "Existing advance",
            Status = "APPROVED",
            ApprovedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedExistingReceiptAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        string receiptNo)
    {
        db.Receipts.Add(new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            ReceiptNo = receiptNo,
            ReceiptDate = new DateOnly(2026, 1, 5),
            AppliedPeriodStart = new DateOnly(2026, 1, 1),
            Amount = 100_000m,
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

    private static MemoryStream BuildReductionWorkbook()
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Invoice");
            WriteHeader(sheet);

            sheet.Cell(2, 1).Value = "2300328765";
            sheet.Cell(2, 2).Value = "0310226744-003";
            sheet.Cell(2, 3).Value = "CHI NHÁNH CÔNG TY TNHH LX PANTOS VIỆT NAM TẠI HẢI PHÒNG";
            sheet.Cell(2, 4).Value = "1C25THK";
            sheet.Cell(2, 5).Value = string.Empty;
            sheet.Cell(2, 6).Value = "2233";
            sheet.Cell(2, 7).Value = "15/07/2025";
            sheet.Cell(2, 8).Value = -2_100_000m;
            sheet.Cell(2, 9).Value = -168_000m;
            sheet.Cell(2, 10).Value = -2_268_000m;
            sheet.Cell(2, 11).Value = "Hóa đơn điều chỉnh giảm";

            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildAdvanceWorkbook(string sellerTaxCode, string customerTaxCode, string advanceNo)
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Data");
            sheet.Cell(1, 1).Value = "SellerTaxCode";
            sheet.Cell(1, 2).Value = "CustomerTaxCode";
            sheet.Cell(1, 3).Value = "AdvanceNo";
            sheet.Cell(1, 4).Value = "AdvanceDate";
            sheet.Cell(1, 5).Value = "Amount";
            sheet.Cell(1, 6).Value = "Description";

            sheet.Cell(2, 1).Value = sellerTaxCode;
            sheet.Cell(2, 2).Value = customerTaxCode;
            sheet.Cell(2, 3).Value = advanceNo;
            sheet.Cell(2, 4).Value = "2026-01-01";
            sheet.Cell(2, 5).Value = 100_000m;
            sheet.Cell(2, 6).Value = "Import advance";

            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildReceiptWorkbook(string sellerTaxCode, string customerTaxCode, string receiptNo)
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Data");
            sheet.Cell(1, 1).Value = "SellerTaxCode";
            sheet.Cell(1, 2).Value = "CustomerTaxCode";
            sheet.Cell(1, 3).Value = "ReceiptNo";
            sheet.Cell(1, 4).Value = "ReceiptDate";
            sheet.Cell(1, 5).Value = "AppliedPeriodStart";
            sheet.Cell(1, 6).Value = "Amount";
            sheet.Cell(1, 7).Value = "Method";
            sheet.Cell(1, 8).Value = "Description";

            sheet.Cell(2, 1).Value = sellerTaxCode;
            sheet.Cell(2, 2).Value = customerTaxCode;
            sheet.Cell(2, 3).Value = receiptNo;
            sheet.Cell(2, 4).Value = "2026-01-05";
            sheet.Cell(2, 5).Value = "2026-01-01";
            sheet.Cell(2, 6).Value = 100_000m;
            sheet.Cell(2, 7).Value = "BANK";
            sheet.Cell(2, 8).Value = "Import receipt";

            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return stream;
    }

    private static void WriteHeader(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "SellerTaxCode";
        sheet.Cell(1, 2).Value = "CustomerTaxCode";
        sheet.Cell(1, 3).Value = "CustomerName";
        sheet.Cell(1, 4).Value = "InvoiceTemplateCode";
        sheet.Cell(1, 5).Value = "InvoiceSeries";
        sheet.Cell(1, 6).Value = "InvoiceNo";
        sheet.Cell(1, 7).Value = "NgayPhatHanh";
        sheet.Cell(1, 8).Value = "RevenueExclVAT";
        sheet.Cell(1, 9).Value = "VatAmount";
        sheet.Cell(1, 10).Value = "TotalAmount";
        sheet.Cell(1, 11).Value = "Note";
    }

    private static IReadOnlyList<string> ReadMessages(string? raw)
    {
        return JsonSerializer.Deserialize<string[]>(raw ?? "[]") ?? Array.Empty<string>();
    }

    private static string? ReadRawString(string? raw, string property)
    {
        using var doc = JsonDocument.Parse(raw ?? "{}");
        return doc.RootElement.TryGetProperty(property, out var value) ? value.GetString() : null;
    }

    private static decimal ReadRawDecimal(string? raw, string property)
    {
        using var doc = JsonDocument.Parse(raw ?? "{}");
        return doc.RootElement.TryGetProperty(property, out var value) ? value.GetDecimal() : 0m;
    }

    private static string FindRepoFile(params string[] relativeSegments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(relativeSegments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException(
            $"Không tìm thấy file kiểm thử cần thiết: {Path.Combine(relativeSegments)}");
    }
}
