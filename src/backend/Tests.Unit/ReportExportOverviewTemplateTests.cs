using System.Reflection;
using ClosedXML.Excel;
using CongNoGolden.Application.Reports;
using CongNoGolden.Infrastructure.Services;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public class ReportExportOverviewTemplateTests
{
    [Fact]
    public void WriteOverview_Builds_Template_Sections_With_Styles()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("TongQuan");

        var kpis = new ReportKpiDto(
            120_000_000m,
            80_000_000m,
            40_000_000m,
            12_500_000m,
            3,
            15_000_000m,
            2,
            6_000_000m,
            1,
            5);

        var insights = new ReportInsightsDto(
            new List<ReportTopCustomerDto>
            {
                new("0101234567", "Công ty ABC", 15_000_000m, 10, 0.2m)
            },
            new List<ReportTopCustomerDto>
            {
                new("0207654321", "Công ty XYZ", 0m, null, null)
            },
            new List<ReportOverdueGroupDto>
            {
                new("owner_1", "Nguyễn Văn A", 12_000_000m, 8_000_000m, 0.6m, 1)
            });

        var method = typeof(ReportExportService).GetMethod(
            "WriteOverview",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        method!.Invoke(
            null,
            new object[]
            {
                sheet,
                kpis,
                insights,
                7,
                new DateOnly(2025, 1, 1),
                new DateOnly(2025, 1, 31)
            });

        var titleRange = sheet.Range("A7:F7");
        Assert.True(titleRange.IsMerged());
        Assert.Equal("TỔNG QUAN CÔNG NỢ", sheet.Cell("A7").GetString());
        Assert.True(sheet.Cell("A7").Style.Font.Bold);

        var headerColor = sheet.Cell("A7").Style.Fill.BackgroundColor.Color.ToArgb();
        Assert.Equal(XLColor.FromHtml("#1E3A5F").Color.ToArgb(), headerColor);

        Assert.Equal("Tổng dư công nợ", sheet.Cell("A10").GetString());
        Assert.Equal(120_000_000m, sheet.Cell("A11").GetValue<decimal>());

        Assert.True(sheet.Range(20, 2, 20, 4).IsMerged());
        Assert.True(sheet.Cell(20, 1).Style.Font.Bold);
    }
}
