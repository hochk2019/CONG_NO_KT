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

    private static async Task SeedExistingReductionInvoiceAsync(ConGNoDbContext db)
    {
        db.Customers.Add(new Customer
        {
            TaxCode = "0310226744",
            Name = "CHI NHÁNH CÔNG TY TNHH LX PANTOS VIỆT NAM TẠI HẢI PHÒNG",
            Status = "ACTIVE",
            CurrentBalance = -2_268_000m,
            PaymentTermsDays = 30,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

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
}
