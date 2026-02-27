using System.Globalization;
using CongNoGolden.Application.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReportExportService
{
    static ReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private async Task<ReportExportResult> ExportSummaryPdfAsync(
        ReportExportRequest request,
        DateOnly from,
        DateOnly to,
        DateOnly asOf,
        CancellationToken ct)
    {
        if (request.Kind != ReportExportKind.Summary)
        {
            throw new InvalidOperationException("Định dạng PDF hiện chỉ hỗ trợ báo cáo tổng hợp.");
        }

        var rows = await _reportService.GetSummaryAsync(
            new ReportSummaryRequest(
                from,
                to,
                "customer",
                request.SellerTaxCode,
                request.CustomerTaxCode,
                request.OwnerId),
            ct);

        var generatedAt = DateTime.Now;
        var fileName = $"CongNo_TongHop_{generatedAt:yyyyMMdd_HHmm}.pdf";
        var content = BuildSummaryPdfDocument(
            rows,
            request,
            from,
            to,
            asOf,
            generatedAt,
            _currentUser.Username ?? "system");

        return new ReportExportResult(content, fileName, "application/pdf");
    }

    private static byte[] BuildSummaryPdfDocument(
        IReadOnlyList<ReportSummaryRow> rows,
        ReportExportRequest request,
        DateOnly from,
        DateOnly to,
        DateOnly asOf,
        DateTime generatedAt,
        string generatedBy)
    {
        var filterText = BuildFilterText(request, from, to);
        var totalInvoiced = rows.Sum(x => x.InvoicedTotal);
        var totalAdvanced = rows.Sum(x => x.AdvancedTotal);
        var totalReceipted = rows.Sum(x => x.ReceiptedTotal);
        var totalOutstandingInvoice = rows.Sum(x => x.OutstandingInvoice);
        var totalOutstandingAdvance = rows.Sum(x => x.OutstandingAdvance);
        var totalCurrentBalance = rows.Sum(x => x.CurrentBalance);

        return Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header().Column(column =>
                    {
                        column.Spacing(4);
                        column.Item().Text("BÁO CÁO TỔNG HỢP CÔNG NỢ").Bold().FontSize(15);
                        column.Item().Text($"Kỳ báo cáo: {from:dd/MM/yyyy} - {to:dd/MM/yyyy}");
                        column.Item().Text($"Tính đến ngày: {asOf:dd/MM/yyyy}");
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Bên bán: {request.SellerTaxCode ?? "Tất cả"}");
                            row.RelativeItem().AlignRight().Text($"Người xuất: {generatedBy}");
                        });
                        if (!string.IsNullOrWhiteSpace(filterText))
                        {
                            column.Item().Text($"Bộ lọc: {filterText}");
                        }
                        column.Item().Text($"Thời gian tạo: {generatedAt:dd/MM/yyyy HH:mm}");
                    });

                    page.Content().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(24);
                            columns.ConstantColumn(90);
                            columns.RelativeColumn(1.6f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.2f);
                        });

                        static IContainer HeaderCell(IContainer container) =>
                            container
                                .Border(1)
                                .BorderColor(Colors.Grey.Lighten1)
                                .Background(Colors.Blue.Lighten4)
                                .PaddingVertical(4)
                                .PaddingHorizontal(4)
                                .AlignCenter()
                                .AlignMiddle();

                        static IContainer BodyCell(IContainer container) =>
                            container
                                .Border(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .PaddingVertical(3)
                                .PaddingHorizontal(4)
                                .AlignMiddle();

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("#").SemiBold();
                            header.Cell().Element(HeaderCell).Text("MST").SemiBold();
                            header.Cell().Element(HeaderCell).Text("Tên").SemiBold();
                            header.Cell().Element(HeaderCell).Text("Phát sinh HĐ").SemiBold();
                            header.Cell().Element(HeaderCell).Text("Phát sinh trả hộ").SemiBold();
                            header.Cell().Element(HeaderCell).Text("Tiền đã thu").SemiBold();
                            header.Cell().Element(HeaderCell).Text("Dư HĐ").SemiBold();
                            header.Cell().Element(HeaderCell).Text("Dư trả hộ").SemiBold();
                            header.Cell().Element(HeaderCell).Text("Tổng nợ").SemiBold();
                        });

                        if (rows.Count == 0)
                        {
                            table.Cell().ColumnSpan(9).Element(BodyCell).AlignCenter().Text("Không có dữ liệu trong kỳ.");
                        }
                        else
                        {
                            for (var index = 0; index < rows.Count; index++)
                            {
                                var row = rows[index];
                                table.Cell()
                                    .Element(BodyCell)
                                    .AlignCenter()
                                    .Text((index + 1).ToString(CultureInfo.InvariantCulture));
                                table.Cell().Element(BodyCell).Text(row.GroupKey);
                                table.Cell().Element(BodyCell).Text(row.GroupName ?? "-");
                                table.Cell().Element(BodyCell).AlignRight().Text(FormatPdfCurrency(row.InvoicedTotal));
                                table.Cell().Element(BodyCell).AlignRight().Text(FormatPdfCurrency(row.AdvancedTotal));
                                table.Cell().Element(BodyCell).AlignRight().Text(FormatPdfCurrency(row.ReceiptedTotal));
                                table.Cell().Element(BodyCell).AlignRight().Text(FormatPdfCurrency(row.OutstandingInvoice));
                                table.Cell().Element(BodyCell).AlignRight().Text(FormatPdfCurrency(row.OutstandingAdvance));
                                table.Cell().Element(BodyCell).AlignRight().Text(FormatPdfCurrency(row.CurrentBalance));
                            }
                        }

                        table.Cell().ColumnSpan(3).Element(BodyCell).AlignRight().Text("Tổng cộng").Bold();
                        table.Cell().Element(BodyCell).AlignRight().Text(FormatPdfCurrency(totalInvoiced)).Bold();
                        table.Cell().Element(BodyCell).AlignRight().Text(FormatPdfCurrency(totalAdvanced)).Bold();
                        table.Cell().Element(BodyCell).AlignRight().Text(FormatPdfCurrency(totalReceipted)).Bold();
                        table.Cell().Element(BodyCell).AlignRight().Text(FormatPdfCurrency(totalOutstandingInvoice)).Bold();
                        table.Cell().Element(BodyCell).AlignRight().Text(FormatPdfCurrency(totalOutstandingAdvance)).Bold();
                        table.Cell().Element(BodyCell).AlignRight().Text(FormatPdfCurrency(totalCurrentBalance)).Bold();
                    });

                    page.Footer().AlignRight().Text(text =>
                    {
                        text.Span("Trang ");
                        text.CurrentPageNumber();
                        text.Span("/");
                        text.TotalPages();
                    });
                });
            })
            .GeneratePdf();
    }

    private static string FormatPdfCurrency(decimal value)
    {
        return string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:N0} đ", value);
    }
}
