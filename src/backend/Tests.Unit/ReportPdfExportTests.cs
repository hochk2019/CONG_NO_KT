using System.Reflection;
using System.Text;
using CongNoGolden.Application.Reports;
using CongNoGolden.Infrastructure.Services;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class ReportPdfExportTests
{
    [Fact]
    public void BuildSummaryPdfDocument_Generates_ValidPdfPayload()
    {
        var rows = new List<ReportSummaryRow>
        {
            new()
            {
                GroupKey = "0101234567",
                GroupName = "Cong ty ABC",
                InvoicedTotal = 150_000_000m,
                AdvancedTotal = 20_000_000m,
                ReceiptedTotal = 100_000_000m,
                OutstandingInvoice = 40_000_000m,
                OutstandingAdvance = 10_000_000m,
                CurrentBalance = 50_000_000m
            }
        };

        var request = new ReportExportRequest(
            From: new DateOnly(2026, 1, 1),
            To: new DateOnly(2026, 1, 31),
            AsOfDate: new DateOnly(2026, 1, 31),
            SellerTaxCode: "0309999999",
            CustomerTaxCode: null,
            OwnerId: null,
            FilterText: "owner=all",
            Kind: ReportExportKind.Summary,
            Format: ReportExportFormat.Pdf);

        var method = typeof(ReportExportService).GetMethod(
            "BuildSummaryPdfDocument",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var payload = (byte[]?)method!.Invoke(
            null,
            new object[]
            {
                rows,
                request,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 1, 31),
                new DateOnly(2026, 1, 31),
                new DateTime(2026, 2, 13, 10, 0, 0),
                "tester"
            });

        Assert.NotNull(payload);
        Assert.NotEmpty(payload);
        Assert.True(payload!.Length > 512);

        var header = Encoding.ASCII.GetString(payload, 0, 5);
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void BuildSummaryPdfDocument_Supports_Vietnamese_TextPayload()
    {
        var rows = new List<ReportSummaryRow>
        {
            new()
            {
                GroupKey = "0310223456",
                GroupName = "Công ty Cổ phần Ánh Dương Việt Nam",
                InvoicedTotal = 12_500_000m,
                AdvancedTotal = 1_250_000m,
                ReceiptedTotal = 8_000_000m,
                OutstandingInvoice = 3_000_000m,
                OutstandingAdvance = 250_000m,
                CurrentBalance = 3_250_000m
            }
        };

        var request = new ReportExportRequest(
            From: new DateOnly(2026, 2, 1),
            To: new DateOnly(2026, 2, 13),
            AsOfDate: new DateOnly(2026, 2, 13),
            SellerTaxCode: "0309999999",
            CustomerTaxCode: null,
            OwnerId: null,
            FilterText: "kênh=nhắc nợ",
            Kind: ReportExportKind.Summary,
            Format: ReportExportFormat.Pdf);

        var method = typeof(ReportExportService).GetMethod(
            "BuildSummaryPdfDocument",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var payload = (byte[]?)method!.Invoke(
            null,
            new object[]
            {
                rows,
                request,
                new DateOnly(2026, 2, 1),
                new DateOnly(2026, 2, 13),
                new DateOnly(2026, 2, 13),
                new DateTime(2026, 2, 13, 10, 0, 0),
                "nguyễn văn a"
            });

        Assert.NotNull(payload);
        Assert.NotEmpty(payload);
        Assert.True(payload!.Length > 512);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(payload, 0, 5));
    }
}
