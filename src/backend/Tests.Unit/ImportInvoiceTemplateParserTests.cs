using System.Text.Json;
using ClosedXML.Excel;
using CongNoGolden.Infrastructure.Services;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public class ImportInvoiceTemplateParserTests
{
    [Fact]
    public void Invoice_Missing_IssueDate_Header_Does_Not_Parse_From_First_Column()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Invoice");
        WriteHeaderMissingIssueDate(sheet);

        sheet.Cell(2, 1).Value = "2025-01-01";
        sheet.Cell(2, 2).Value = "CUST01";
        sheet.Cell(2, 3).Value = "INV-01";
        sheet.Cell(2, 4).Value = 1000;

        var rows = ImportInvoiceTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid());

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusError, rows[0].ValidationStatus);

        var messages = ReadMessages(rows[0].ValidationMessages);
        Assert.Contains("ISSUE_DATE_REQUIRED", messages);
    }

    [Fact]
    public void Invoice_IssueDate_String_DdMmYyyy_Is_Parsed()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Invoice");
        WriteFullHeader(sheet);

        sheet.Cell(2, 1).Value = "SELLER01";
        sheet.Cell(2, 2).Value = "CUST01";
        sheet.Cell(2, 3).Value = "Buyer A";
        sheet.Cell(2, 4).Value = "01GTKT";
        sheet.Cell(2, 5).Value = "AA/23E";
        sheet.Cell(2, 6).Value = "INV001";
        sheet.Cell(2, 7).Value = "15/02/2025";
        sheet.Cell(2, 8).Value = 100;
        sheet.Cell(2, 9).Value = 10;
        sheet.Cell(2, 10).Value = 110;

        var rows = ImportInvoiceTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid());

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusOk, rows[0].ValidationStatus);

        Assert.Equal("2025-02-15", ReadRawString(rows[0].RawData, "issue_date"));
    }

    [Fact]
    public void Negative_Reduction_Adjustment_Row_Is_Accepted_In_Template_Import()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Invoice");
        WriteFullHeader(sheet);

        sheet.Cell(2, 1).Value = "0102030405";
        sheet.Cell(2, 2).Value = "0310226744-003";
        sheet.Cell(2, 3).Value = "Buyer Reduction";
        sheet.Cell(2, 4).Value = "01GTKT";
        sheet.Cell(2, 5).Value = "AA/25E";
        sheet.Cell(2, 6).Value = "ADJ001";
        sheet.Cell(2, 7).Value = "15/02/2025";
        sheet.Cell(2, 8).Value = -200m;
        sheet.Cell(2, 9).Value = -20m;
        sheet.Cell(2, 10).Value = -220m;
        sheet.Cell(2, 11).Value = "Hóa đơn điều chỉnh giảm";

        var rows = ImportInvoiceTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid());

        Assert.Single(rows);
        Assert.Equal("INSERT", rows[0].ActionSuggestion);
        Assert.NotEqual(ImportStagingHelpers.StatusError, rows[0].ValidationStatus);

        var messages = ReadMessages(rows[0].ValidationMessages);
        Assert.DoesNotContain("NEGATIVE_AMOUNT", messages);

        Assert.Equal("0310226744-003", ReadRawString(rows[0].RawData, "customer_tax_code"));
        Assert.Equal("0310226744", ReadRawString(rows[0].RawData, "customer_tax_code_matching"));
        Assert.Equal("ADJUSTMENT_REDUCTION", ReadRawString(rows[0].RawData, "invoice_type"));
    }

    [Fact]
    public void Invoice_SystemTemplateFile_Parses_IssueDate_From_Data_Sheet()
    {
        var templatePath = FindRepoFile("src", "frontend", "public", "templates", "invoice_template.xlsx");

        using var workbook = new XLWorkbook(templatePath);
        var sheet = workbook.Worksheet("Data");

        var rows = ImportInvoiceTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid());

        Assert.Single(rows);

        var messages = ReadMessages(rows[0].ValidationMessages);
        Assert.DoesNotContain("ISSUE_DATE_REQUIRED", messages);
        Assert.Equal("2300328765", ReadRawString(rows[0].RawData, "seller_tax_code"));
        Assert.Equal("0101000002", ReadRawString(rows[0].RawData, "customer_tax_code"));
        Assert.Equal("2026-01-15", ReadRawString(rows[0].RawData, "issue_date"));
        Assert.Equal("Cong ty TNHH Mau", ReadRawString(rows[0].RawData, "customer_name"));
        Assert.Equal("1C25THK", ReadRawString(rows[0].RawData, "invoice_template_code"));
        Assert.Equal("AA/26E", ReadRawString(rows[0].RawData, "invoice_series"));
        Assert.Equal("0000123", ReadRawString(rows[0].RawData, "invoice_no"));
        Assert.Equal(10000000m, ReadRawDecimal(rows[0].RawData, "revenue_excl_vat"));
        Assert.Equal(800000m, ReadRawDecimal(rows[0].RawData, "vat_amount"));
        Assert.Equal(10800000m, ReadRawDecimal(rows[0].RawData, "total_amount"));
        Assert.Equal("Hoa don mau cho doi soat", ReadRawString(rows[0].RawData, "note"));
    }

    private static void WriteHeaderMissingIssueDate(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "SellerTaxCode";
        sheet.Cell(1, 2).Value = "CustomerTaxCode";
        sheet.Cell(1, 3).Value = "InvoiceNo";
        sheet.Cell(1, 4).Value = "TotalAmount";
    }

    private static void WriteFullHeader(IXLWorksheet sheet)
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
