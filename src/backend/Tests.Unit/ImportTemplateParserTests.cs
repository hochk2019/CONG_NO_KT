using System.Text.Json;
using ClosedXML.Excel;
using CongNoGolden.Infrastructure.Services;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public class ImportTemplateParserTests
{
    [Fact]
    public void Receipt_Missing_Fields_Returns_Error()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Receipt");
        WriteReceiptHeader(sheet);

        sheet.Cell(2, 2).Value = "CUST01";
        sheet.Cell(2, 3).Value = 0;
        sheet.Cell(2, 4).Value = new DateTime(2025, 1, 10);

        var rows = ImportTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid(), ImportTemplateType.Receipt);

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusError, rows[0].ValidationStatus);

        var messages = ReadMessages(rows[0].ValidationMessages);
        Assert.Contains("SELLER_TAX_REQUIRED", messages);
        Assert.Contains("AMOUNT_REQUIRED", messages);
        Assert.Contains("APPLIED_PERIOD_REQUIRED", messages);
    }

    [Fact]
    public void Receipt_Missing_Amount_Header_Does_Not_Parse_From_First_Column()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Receipt");
        WriteReceiptHeaderMissingAmount(sheet);

        sheet.Cell(2, 1).Value = "2301098313";
        sheet.Cell(2, 2).Value = "CUST01";
        sheet.Cell(2, 3).Value = new DateTime(2025, 1, 10);
        sheet.Cell(2, 4).Value = new DateTime(2025, 1, 1);
        sheet.Cell(2, 5).Value = "BANK";

        var rows = ImportTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid(), ImportTemplateType.Receipt);

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusError, rows[0].ValidationStatus);

        var messages = ReadMessages(rows[0].ValidationMessages);
        Assert.Contains("AMOUNT_REQUIRED", messages);
        Assert.DoesNotContain("RECEIPT_DATE_REQUIRED", messages);
        Assert.DoesNotContain("APPLIED_PERIOD_REQUIRED", messages);
    }

    [Fact]
    public void Receipt_Invalid_Method_And_Period_Not_First_Day_Returns_Warn()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Receipt");
        WriteReceiptHeader(sheet);

        sheet.Cell(2, 1).Value = "SELLER01";
        sheet.Cell(2, 2).Value = "CUST01";
        sheet.Cell(2, 3).Value = 100;
        sheet.Cell(2, 4).Value = new DateTime(2025, 1, 10);
        sheet.Cell(2, 5).Value = new DateTime(2025, 1, 15);
        sheet.Cell(2, 6).Value = "INVALID";

        var rows = ImportTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid(), ImportTemplateType.Receipt);

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusWarn, rows[0].ValidationStatus);

        var messages = ReadMessages(rows[0].ValidationMessages);
        Assert.Contains("METHOD_INVALID", messages);
        Assert.Contains("APPLIED_PERIOD_NOT_FIRST_DAY", messages);
    }

    [Fact]
    public void Receipt_Date_String_DdMmYyyy_Is_Parsed()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Receipt");
        WriteReceiptHeader(sheet);

        sheet.Cell(2, 1).Value = "SELLER01";
        sheet.Cell(2, 2).Value = "CUST01";
        sheet.Cell(2, 3).Value = 1000;
        sheet.Cell(2, 4).Value = "31/12/2025";
        sheet.Cell(2, 5).Value = "01/12/2025";
        sheet.Cell(2, 6).Value = "BANK";

        var rows = ImportTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid(), ImportTemplateType.Receipt);

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusOk, rows[0].ValidationStatus);

        Assert.Equal("2025-12-31", ReadRawString(rows[0].RawData, "receipt_date"));
        Assert.Equal("2025-12-01", ReadRawString(rows[0].RawData, "applied_period_start"));
    }

    [Fact]
    public void Advance_Date_String_DdDashMmDashYyyy_Is_Parsed()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Advance");
        WriteAdvanceHeader(sheet);

        sheet.Cell(2, 1).Value = "SELLER01";
        sheet.Cell(2, 2).Value = "CUST01";
        sheet.Cell(2, 3).Value = 150;
        sheet.Cell(2, 4).Value = "05-02-2025";

        var rows = ImportTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid(), ImportTemplateType.Advance);

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusOk, rows[0].ValidationStatus);

        Assert.Equal("2025-02-05", ReadRawString(rows[0].RawData, "advance_date"));
    }

    [Fact]
    public void Advance_Missing_Date_Returns_Error()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Advance");
        WriteAdvanceHeader(sheet);

        sheet.Cell(2, 1).Value = "SELLER01";
        sheet.Cell(2, 2).Value = "CUST01";
        sheet.Cell(2, 3).Value = 150;

        var rows = ImportTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid(), ImportTemplateType.Advance);

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusError, rows[0].ValidationStatus);

        var messages = ReadMessages(rows[0].ValidationMessages);
        Assert.Contains("ADVANCE_DATE_REQUIRED", messages);
    }

    private static void WriteReceiptHeader(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "SellerTaxCode";
        sheet.Cell(1, 2).Value = "CustomerTaxCode";
        sheet.Cell(1, 3).Value = "Amount";
        sheet.Cell(1, 4).Value = "ReceiptDate";
        sheet.Cell(1, 5).Value = "AppliedPeriodStart";
        sheet.Cell(1, 6).Value = "Method";
        sheet.Cell(1, 7).Value = "Description";
    }

    private static void WriteReceiptHeaderMissingAmount(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "SellerTaxCode";
        sheet.Cell(1, 2).Value = "CustomerTaxCode";
        sheet.Cell(1, 3).Value = "ReceiptDate";
        sheet.Cell(1, 4).Value = "AppliedPeriodStart";
        sheet.Cell(1, 5).Value = "Method";
        sheet.Cell(1, 6).Value = "Description";
    }

    private static void WriteAdvanceHeader(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "SellerTaxCode";
        sheet.Cell(1, 2).Value = "CustomerTaxCode";
        sheet.Cell(1, 3).Value = "Amount";
        sheet.Cell(1, 4).Value = "AdvanceDate";
        sheet.Cell(1, 5).Value = "Description";
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
}
