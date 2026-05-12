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
        sheet.Cell(2, 3).Value = "PT-001";
        sheet.Cell(2, 4).Value = new DateTime(2025, 1, 10);
        sheet.Cell(2, 6).Value = 0;

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
        sheet.Cell(2, 3).Value = "PT-001";
        sheet.Cell(2, 4).Value = new DateTime(2025, 1, 10);
        sheet.Cell(2, 5).Value = new DateTime(2025, 1, 1);
        sheet.Cell(2, 6).Value = "BANK";

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
        sheet.Cell(2, 3).Value = "PT-001";
        sheet.Cell(2, 4).Value = new DateTime(2025, 1, 10);
        sheet.Cell(2, 5).Value = new DateTime(2025, 1, 15);
        sheet.Cell(2, 6).Value = 100;
        sheet.Cell(2, 7).Value = "INVALID";

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
        sheet.Cell(2, 3).Value = "PT-001";
        sheet.Cell(2, 4).Value = "31/12/2025";
        sheet.Cell(2, 5).Value = "01/12/2025";
        sheet.Cell(2, 6).Value = 1000;
        sheet.Cell(2, 7).Value = "BANK";

        var rows = ImportTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid(), ImportTemplateType.Receipt);

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusOk, rows[0].ValidationStatus);

        Assert.Equal("2025-12-31", ReadRawString(rows[0].RawData, "receipt_date"));
        Assert.Equal("2025-12-01", ReadRawString(rows[0].RawData, "applied_period_start"));
    }

    [Fact]
    public void Receipt_Ambiguous_Vietnamese_Date_String_Is_Not_Swapped()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Receipt");
        WriteReceiptHeader(sheet);

        sheet.Cell(2, 1).Value = "SELLER01";
        sheet.Cell(2, 2).Value = "CUST01";
        sheet.Cell(2, 3).Value = "PT-AMBIG";
        sheet.Cell(2, 4).Value = "07/04/2026";
        sheet.Cell(2, 5).Value = "01/04/2026";
        sheet.Cell(2, 6).Value = 1000;
        sheet.Cell(2, 7).Value = "BANK";

        var rows = ImportTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid(), ImportTemplateType.Receipt);

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusOk, rows[0].ValidationStatus);
        Assert.Equal("2026-04-07", ReadRawString(rows[0].RawData, "receipt_date"));
        Assert.Equal("2026-04-01", ReadRawString(rows[0].RawData, "applied_period_start"));
    }

    [Fact]
    public void Receipt_Formatted_Tax_Code_Cell_Preserves_Leading_Zero()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Receipt");
        WriteReceiptHeader(sheet);

        sheet.Cell(2, 1).Value = "2300328765";
        sheet.Cell(2, 2).Value = 106733173;
        sheet.Cell(2, 2).Style.NumberFormat.Format = "0000000000";
        sheet.Cell(2, 3).Value = "PT-ZERO";
        sheet.Cell(2, 4).Value = "11/05/2026";
        sheet.Cell(2, 5).Value = "01/05/2026";
        sheet.Cell(2, 6).Value = 1000;
        sheet.Cell(2, 7).Value = "BANK";

        var rows = ImportTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid(), ImportTemplateType.Receipt);

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusOk, rows[0].ValidationStatus);
        Assert.Equal("0106733173", ReadRawString(rows[0].RawData, "customer_tax_code"));
    }

    [Fact]
    public void Receipt_Missing_DocumentNo_Returns_Error()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Receipt");
        WriteReceiptHeader(sheet);

        sheet.Cell(2, 1).Value = "SELLER01";
        sheet.Cell(2, 2).Value = "CUST01";
        sheet.Cell(2, 4).Value = new DateTime(2025, 12, 31);
        sheet.Cell(2, 5).Value = new DateTime(2025, 12, 1);
        sheet.Cell(2, 6).Value = 1000;

        var rows = ImportTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid(), ImportTemplateType.Receipt);

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusError, rows[0].ValidationStatus);
        Assert.Contains("RECEIPT_NO_REQUIRED", ReadMessages(rows[0].ValidationMessages));
    }

    [Fact]
    public void Receipt_Duplicate_DocumentNo_InFile_Returns_Warn_And_Skip()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Receipt");
        WriteReceiptHeader(sheet);

        sheet.Cell(2, 1).Value = "SELLER01";
        sheet.Cell(2, 2).Value = "CUST01";
        sheet.Cell(2, 3).Value = "PT-DUP";
        sheet.Cell(2, 4).Value = new DateTime(2025, 12, 31);
        sheet.Cell(2, 5).Value = new DateTime(2025, 12, 1);
        sheet.Cell(2, 6).Value = 1000;
        sheet.Cell(2, 7).Value = "BANK";

        sheet.Cell(3, 1).Value = "SELLER01";
        sheet.Cell(3, 2).Value = "CUST01";
        sheet.Cell(3, 3).Value = "PT-DUP";
        sheet.Cell(3, 4).Value = new DateTime(2026, 1, 5);
        sheet.Cell(3, 5).Value = new DateTime(2026, 1, 1);
        sheet.Cell(3, 6).Value = 2000;
        sheet.Cell(3, 7).Value = "CASH";

        var rows = ImportTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid(), ImportTemplateType.Receipt);

        Assert.Equal(2, rows.Count);
        Assert.Equal(ImportStagingHelpers.StatusWarn, rows[1].ValidationStatus);
        Assert.Equal("SKIP", rows[1].ActionSuggestion);
        Assert.Contains("DUP_IN_FILE", ReadMessages(rows[1].ValidationMessages));
    }

    [Fact]
    public void Advance_Date_String_DdDashMmDashYyyy_Is_Parsed()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Advance");
        WriteAdvanceHeader(sheet);

        sheet.Cell(2, 1).Value = "SELLER01";
        sheet.Cell(2, 2).Value = "CUST01";
        sheet.Cell(2, 3).Value = "ADV-001";
        sheet.Cell(2, 4).Value = "05-02-2025";
        sheet.Cell(2, 5).Value = 150;

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
        sheet.Cell(2, 3).Value = "ADV-001";
        sheet.Cell(2, 5).Value = 150;

        var rows = ImportTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid(), ImportTemplateType.Advance);

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusError, rows[0].ValidationStatus);

        var messages = ReadMessages(rows[0].ValidationMessages);
        Assert.Contains("ADVANCE_DATE_REQUIRED", messages);
    }

    [Fact]
    public void Advance_Missing_DocumentNo_Returns_Error()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Advance");
        WriteAdvanceHeader(sheet);

        sheet.Cell(2, 1).Value = "SELLER01";
        sheet.Cell(2, 2).Value = "CUST01";
        sheet.Cell(2, 4).Value = new DateTime(2025, 2, 5);
        sheet.Cell(2, 5).Value = 150;

        var rows = ImportTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid(), ImportTemplateType.Advance);

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusError, rows[0].ValidationStatus);
        Assert.Contains("ADVANCE_NO_REQUIRED", ReadMessages(rows[0].ValidationMessages));
    }

    [Fact]
    public void Advance_Duplicate_DocumentNo_InFile_Returns_Warn_And_Skip()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Advance");
        WriteAdvanceHeader(sheet);

        sheet.Cell(2, 1).Value = "SELLER01";
        sheet.Cell(2, 2).Value = "CUST01";
        sheet.Cell(2, 3).Value = "ADV-DUP";
        sheet.Cell(2, 4).Value = new DateTime(2025, 2, 5);
        sheet.Cell(2, 5).Value = 150;

        sheet.Cell(3, 1).Value = "SELLER01";
        sheet.Cell(3, 2).Value = "CUST01";
        sheet.Cell(3, 3).Value = "ADV-DUP";
        sheet.Cell(3, 4).Value = new DateTime(2025, 2, 8);
        sheet.Cell(3, 5).Value = 250;

        var rows = ImportTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid(), ImportTemplateType.Advance);

        Assert.Equal(2, rows.Count);
        Assert.Equal(ImportStagingHelpers.StatusWarn, rows[1].ValidationStatus);
        Assert.Equal("SKIP", rows[1].ActionSuggestion);
        Assert.Contains("DUP_IN_FILE", ReadMessages(rows[1].ValidationMessages));
    }

    [Fact]
    public void Advance_SystemTemplateFile_Parses_All_SnakeCase_Columns()
    {
        var templatePath = FindRepoFile("src", "frontend", "public", "templates", "advance_template.xlsx");

        using var workbook = new XLWorkbook(templatePath);
        var sheet = workbook.Worksheet("Data");

        var rows = ImportTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid(), ImportTemplateType.Advance);

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusOk, rows[0].ValidationStatus);

        var messages = ReadMessages(rows[0].ValidationMessages);
        Assert.DoesNotContain("ADVANCE_DATE_REQUIRED", messages);
        Assert.Equal("2300328765", ReadRawString(rows[0].RawData, "seller_tax_code"));
        Assert.Equal("0101000002", ReadRawString(rows[0].RawData, "customer_tax_code"));
        Assert.Equal("TH-2026-0001", ReadRawString(rows[0].RawData, "advance_no"));
        Assert.Equal("2026-01-10", ReadRawString(rows[0].RawData, "advance_date"));
        Assert.Equal(1500000m, ReadRawDecimal(rows[0].RawData, "amount"));
        Assert.Equal("Tra ho cuoc van chuyen", ReadRawString(rows[0].RawData, "description"));
    }

    [Fact]
    public void Receipt_SystemTemplateFile_Parses_All_SnakeCase_Columns()
    {
        var templatePath = FindRepoFile("src", "frontend", "public", "templates", "receipt_template.xlsx");

        using var workbook = new XLWorkbook(templatePath);
        var sheet = workbook.Worksheet("Data");

        var rows = ImportTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid(), ImportTemplateType.Receipt);

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusOk, rows[0].ValidationStatus);

        var messages = ReadMessages(rows[0].ValidationMessages);
        Assert.DoesNotContain("RECEIPT_DATE_REQUIRED", messages);
        Assert.DoesNotContain("APPLIED_PERIOD_REQUIRED", messages);
        Assert.Equal("2300328765", ReadRawString(rows[0].RawData, "seller_tax_code"));
        Assert.Equal("0101000002", ReadRawString(rows[0].RawData, "customer_tax_code"));
        Assert.Equal("PT-2026-0001", ReadRawString(rows[0].RawData, "receipt_no"));
        Assert.Equal("2026-01-12", ReadRawString(rows[0].RawData, "receipt_date"));
        Assert.Equal("2026-01-01", ReadRawString(rows[0].RawData, "applied_period_start"));
        Assert.Equal(2500000m, ReadRawDecimal(rows[0].RawData, "amount"));
        Assert.Equal("BANK", ReadRawString(rows[0].RawData, "method"));
        Assert.Equal("Thu cong no thang 01/2026", ReadRawString(rows[0].RawData, "description"));
    }

    private static void WriteReceiptHeader(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "SellerTaxCode";
        sheet.Cell(1, 2).Value = "CustomerTaxCode";
        sheet.Cell(1, 3).Value = "ReceiptNo";
        sheet.Cell(1, 4).Value = "ReceiptDate";
        sheet.Cell(1, 5).Value = "AppliedPeriodStart";
        sheet.Cell(1, 6).Value = "Amount";
        sheet.Cell(1, 7).Value = "Method";
        sheet.Cell(1, 8).Value = "Description";
    }

    private static void WriteReceiptHeaderMissingAmount(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "SellerTaxCode";
        sheet.Cell(1, 2).Value = "CustomerTaxCode";
        sheet.Cell(1, 3).Value = "ReceiptNo";
        sheet.Cell(1, 4).Value = "ReceiptDate";
        sheet.Cell(1, 5).Value = "AppliedPeriodStart";
        sheet.Cell(1, 6).Value = "Method";
        sheet.Cell(1, 7).Value = "Description";
    }

    private static void WriteAdvanceHeader(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "SellerTaxCode";
        sheet.Cell(1, 2).Value = "CustomerTaxCode";
        sheet.Cell(1, 3).Value = "AdvanceNo";
        sheet.Cell(1, 4).Value = "AdvanceDate";
        sheet.Cell(1, 5).Value = "Amount";
        sheet.Cell(1, 6).Value = "Description";
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
