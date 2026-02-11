using ClosedXML.Excel;
using CongNoGolden.Application.Reports;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReportExportService
{
    private const string ShortDateFormat = "dd/MM/yyyy";
    private const string DateTimeFormat = "dd/MM/yyyy HH:mm";

    private void FillHeader(
        IXLWorksheet sheet,
        ReportExportRequest request,
        string? sellerName,
        DateOnly from,
        DateOnly to,
        DateOnly asOf)
    {
        sheet.Cell("B2").Value = request.SellerTaxCode ?? string.Empty;
        sheet.Cell("F2").Value = sellerName ?? string.Empty;

        var fromCell = sheet.Cell("B3");
        if (request.From.HasValue)
        {
            fromCell.Value = from.ToDateTime(TimeOnly.MinValue);
            fromCell.Style.DateFormat.Format = ShortDateFormat;
        }
        else
        {
            fromCell.Value = string.Empty;
        }

        var toCell = sheet.Cell("D3");
        if (request.To.HasValue)
        {
            toCell.Value = to.ToDateTime(TimeOnly.MinValue);
            toCell.Style.DateFormat.Format = ShortDateFormat;
        }
        else
        {
            toCell.Value = string.Empty;
        }

        sheet.Cell("F3").Value = BuildPeriodLabel(request, from, to);

        sheet.Cell("B4").Value = BuildFilterText(request, from, to);
        var generatedCell = sheet.Cell("B5");
        generatedCell.Value = DateTime.Now;
        generatedCell.Style.DateFormat.Format = DateTimeFormat;

        sheet.Cell("F5").Value = _currentUser.Username ?? "system";

        if (sheet.Name.Equals("Aging", StringComparison.OrdinalIgnoreCase))
        {
            var asOfCell = sheet.Cell("B6");
            asOfCell.Value = asOf.ToDateTime(TimeOnly.MinValue);
            asOfCell.Style.DateFormat.Format = ShortDateFormat;
        }

        if (sheet.Name.Equals("TongQuan", StringComparison.OrdinalIgnoreCase))
        {
            sheet.Cell("A6").Value = "Loại báo cáo:";
            sheet.Cell("B6").Value = "Báo cáo tổng quan";
        }

        ApplyHeaderAreaLayout(sheet);
        ApplyReportTitleLayout(sheet);
    }

    private static string BuildPeriodLabel(ReportExportRequest request, DateOnly from, DateOnly to)
    {
        if (!request.From.HasValue || !request.To.HasValue)
        {
            return string.Empty;
        }

        return from.Month == to.Month && from.Year == to.Year
            ? $"{from:yyyy-MM}"
            : string.Empty;
    }

    private static string BuildFilterText(ReportExportRequest request, DateOnly from, DateOnly to)
    {
        if (!string.IsNullOrWhiteSpace(request.FilterText))
        {
            return request.FilterText;
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.SellerTaxCode))
        {
            parts.Add($"seller={request.SellerTaxCode}");
        }
        if (!string.IsNullOrWhiteSpace(request.CustomerTaxCode))
        {
            parts.Add($"customer={request.CustomerTaxCode}");
        }
        if (request.OwnerId.HasValue)
        {
            parts.Add($"owner={request.OwnerId}");
        }
        if (request.From.HasValue || request.To.HasValue)
        {
            parts.Add($"period={from:yyyy-MM-dd}..{to:yyyy-MM-dd}");
        }

        return string.Join("; ", parts);
    }

    private static void WriteSummary(IXLWorksheet sheet, IReadOnlyList<ExportSummaryRow> rows)
    {
        ClearRows(sheet, 8);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowIndex = 8 + i;

            sheet.Cell(rowIndex, 1).Value = i + 1;
            sheet.Cell(rowIndex, 2).Value = row.CustomerTaxCode;
            sheet.Cell(rowIndex, 3).Value = row.CustomerName;
            sheet.Cell(rowIndex, 4).Value = row.OwnerName ?? string.Empty;
            sheet.Cell(rowIndex, 5).Value = row.OpeningBalance;
            sheet.Cell(rowIndex, 6).Value = row.InvoicedTotal;
            sheet.Cell(rowIndex, 7).Value = row.AdvancedTotal;
            sheet.Cell(rowIndex, 8).Value = row.ReceiptedTotal;
            sheet.Cell(rowIndex, 9).Value = row.Adjustments;
            sheet.Cell(rowIndex, 10).Value = row.ClosingBalance;
            sheet.Cell(rowIndex, 11).Value = row.OverdueAmount;
            sheet.Cell(rowIndex, 12).Value = row.MaxAgeDays;
            sheet.Cell(rowIndex, 13).Value = string.Empty;

            CopyRowStyle(sheet, 8, rowIndex);
        }

        ApplySummaryLayout(sheet, rows.Count);
    }

    private static void WriteDetails(IXLWorksheet sheet, IReadOnlyList<ReportStatementLine> lines)
    {
        ClearRows(sheet, 8);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var rowIndex = 8 + i;

            CopyRowStyle(sheet, 8, rowIndex);

            sheet.Cell(rowIndex, 1).Value = i + 1;

            var documentCell = sheet.Cell(rowIndex, 2);
            documentCell.Value = line.DocumentDate.ToDateTime(TimeOnly.MinValue);
            documentCell.Style.DateFormat.Format = ShortDateFormat;

            var appliedCell = sheet.Cell(rowIndex, 3);
            if (line.AppliedPeriodStart.HasValue)
            {
                appliedCell.Value = line.AppliedPeriodStart.Value.ToDateTime(TimeOnly.MinValue);
                appliedCell.Style.DateFormat.Format = ShortDateFormat;
            }
            else
            {
                appliedCell.Value = string.Empty;
            }

            sheet.Cell(rowIndex, 4).Value = line.Type;
            sheet.Cell(rowIndex, 5).Value = line.SellerTaxCode;
            sheet.Cell(rowIndex, 6).Value = line.CustomerTaxCode;
            sheet.Cell(rowIndex, 7).Value = line.CustomerName;
            sheet.Cell(rowIndex, 8).Value = line.DocumentNo ?? string.Empty;
            sheet.Cell(rowIndex, 9).Value = line.Description ?? string.Empty;
            sheet.Cell(rowIndex, 10).Value = line.Revenue;
            sheet.Cell(rowIndex, 11).Value = line.Vat;
            sheet.Cell(rowIndex, 12).Value = line.Increase;
            sheet.Cell(rowIndex, 13).Value = line.Decrease;
            sheet.Cell(rowIndex, 14).Value = line.RunningBalance;
            sheet.Cell(rowIndex, 15).Value = line.CreatedBy ?? string.Empty;
            sheet.Cell(rowIndex, 16).Value = line.ApprovedBy ?? string.Empty;
            sheet.Cell(rowIndex, 17).Value = line.Batch ?? string.Empty;
        }

        ApplyDetailsLayout(sheet, lines.Count);
    }

    private static void WriteAging(IXLWorksheet sheet, IReadOnlyList<ReportAgingRow> rows)
    {
        ClearRows(sheet, 8);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowIndex = 8 + i;

            sheet.Cell(rowIndex, 1).Value = i + 1;
            sheet.Cell(rowIndex, 2).Value = row.CustomerTaxCode;
            sheet.Cell(rowIndex, 3).Value = row.CustomerName;
            sheet.Cell(rowIndex, 4).Value = row.SellerTaxCode;
            sheet.Cell(rowIndex, 5).Value = row.Bucket0To30;
            sheet.Cell(rowIndex, 6).Value = row.Bucket31To60;
            sheet.Cell(rowIndex, 7).Value = row.Bucket61To90;
            sheet.Cell(rowIndex, 8).Value = row.Bucket91To180;
            sheet.Cell(rowIndex, 9).Value = row.BucketOver180;
            sheet.Cell(rowIndex, 10).Value = row.Total;
            sheet.Cell(rowIndex, 11).Value = row.Overdue;
            sheet.Cell(rowIndex, 12).Value = string.Empty;

            CopyRowStyle(sheet, 8, rowIndex);
        }

        ApplyAgingLayout(sheet, rows.Count);
    }

    private static void WriteOverview(
        IXLWorksheet sheet,
        ReportKpiDto kpis,
        ReportInsightsDto insights,
        int dueSoonDays,
        DateOnly from,
        DateOnly to)
    {
        ResetOverviewArea(sheet, 7, 60, 6);

        sheet.Rows(1, 6).Hide();
        sheet.SheetView.FreezeRows(7);
        ApplyOverviewColumnWidths(sheet);

        var titleRange = sheet.Range(7, 1, 7, 6);
        titleRange.Merge();
        titleRange.Value = "TỔNG QUAN CÔNG NỢ";
        ApplyTitleStyle(titleRange);
        sheet.Row(7).Height = 28;

        var subtitleRange = sheet.Range(8, 1, 8, 6);
        subtitleRange.Merge();
        subtitleRange.Value = $"Kỳ báo cáo: {from:dd/MM/yyyy} - {to:dd/MM/yyyy}";
        ApplySubtitleStyle(subtitleRange);
        sheet.Row(8).Height = 20;

        WriteKpiCard(sheet, 10, 1, "Tổng dư công nợ", kpis.TotalOutstanding);
        WriteKpiCard(sheet, 10, 4, "Dư hóa đơn", kpis.OutstandingInvoice);
        WriteKpiCard(sheet, 12, 1, "Dư khoản trả hộ", kpis.OutstandingAdvance);
        WriteKpiCard(sheet, 12, 4, "Đã thu chưa phân bổ", kpis.UnallocatedReceiptsAmount);
        WriteKpiCard(sheet, 14, 1, "Quá hạn", kpis.OverdueAmount);
        WriteKpiCard(sheet, 14, 4, $"Sắp đến hạn ({dueSoonDays} ngày)", kpis.DueSoonAmount);
        WriteKpiCard(sheet, 16, 1, "KH trả đúng hạn", kpis.OnTimeCustomers);
        WriteKpiCard(sheet, 16, 4, "KH quá hạn", kpis.OverdueCustomers);

        var row = 19;
        row = WriteTopList(sheet, row, "TOP CẦN CHÚ Ý", insights.TopOutstanding);
        row += 1;
        row = WriteTopList(sheet, row, "TOP TRẢ ĐÚNG HẠN NHẤT", insights.TopOnTime);
        row += 1;
        WriteOwnerList(sheet, row, "QUÁ HẠN THEO PHỤ TRÁCH", insights.OverdueByOwner);
    }

    private static int WriteTopList(
        IXLWorksheet sheet,
        int startRow,
        string title,
        IReadOnlyList<ReportTopCustomerDto> rows)
    {
        var row = startRow;
        var titleRange = sheet.Range(row, 1, row, 6);
        titleRange.Merge();
        titleRange.Value = title;
        ApplySectionTitleStyle(titleRange);
        row += 1;

        sheet.Cell(row, 1).Value = "MST";
        var nameHeader = sheet.Range(row, 2, row, 4);
        nameHeader.Merge();
        nameHeader.Value = "Tên khách hàng";
        var amountHeader = sheet.Range(row, 5, row, 6);
        amountHeader.Merge();
        amountHeader.Value = "Giá trị";
        ApplyTableHeaderStyle(sheet.Range(row, 1, row, 6));
        row += 1;

        var dataStartRow = row;
        foreach (var item in rows)
        {
            sheet.Cell(row, 1).Value = item.CustomerTaxCode;
            var nameCell = sheet.Range(row, 2, row, 4);
            nameCell.Merge();
            nameCell.Value = item.CustomerName;
            var amountCell = sheet.Range(row, 5, row, 6);
            amountCell.Merge();
            amountCell.Value = item.Amount;
            SetCurrencyFormat(amountCell);
            ApplyTableRowStyle(sheet.Range(row, 1, row, 6), row % 2 == 0);
            row += 1;
        }

        if (rows.Count == 0)
        {
            var emptyRange = sheet.Range(row, 1, row, 6);
            emptyRange.Merge();
            emptyRange.Value = "Không có dữ liệu.";
            emptyRange.Style.Font.Italic = true;
            ApplyTableRowStyle(emptyRange, false);
            row += 1;
        }
        else
        {
            var dataEndRow = row - 1;
            var nameRange = sheet.Range(dataStartRow, 2, dataEndRow, 4);
            nameRange.Style.Alignment.WrapText = true;
            sheet.Rows(dataStartRow, dataEndRow).AdjustToContents(18, 36);
            sheet.Columns(2, 4).AdjustToContents(14, 36);
        }

        return row;
    }

    private static int WriteOwnerList(
        IXLWorksheet sheet,
        int startRow,
        string title,
        IReadOnlyList<ReportOverdueGroupDto> rows)
    {
        var row = startRow;
        var titleRange = sheet.Range(row, 1, row, 6);
        titleRange.Merge();
        titleRange.Value = title;
        ApplySectionTitleStyle(titleRange);
        row += 1;

        var ownerHeader = sheet.Range(row, 1, row, 4);
        ownerHeader.Merge();
        ownerHeader.Value = "Phụ trách";
        sheet.Cell(row, 5).Value = "Dư quá hạn";
        sheet.Cell(row, 6).Value = "Số KH quá hạn";
        ApplyTableHeaderStyle(sheet.Range(row, 1, row, 6));
        row += 1;

        var dataStartRow = row;
        foreach (var item in rows)
        {
            var ownerCell = sheet.Range(row, 1, row, 4);
            ownerCell.Merge();
            ownerCell.Value = item.GroupName;
            var amountCell = sheet.Range(row, 5, row, 5);
            amountCell.Value = item.OverdueAmount;
            SetCurrencyFormat(amountCell);
            var countCell = sheet.Range(row, 6, row, 6);
            countCell.Value = item.OverdueCustomers;
            SetIntegerFormat(countCell);
            ApplyTableRowStyle(sheet.Range(row, 1, row, 6), row % 2 == 0);
            row += 1;
        }

        if (rows.Count == 0)
        {
            var emptyRange = sheet.Range(row, 1, row, 6);
            emptyRange.Merge();
            emptyRange.Value = "Không có dữ liệu.";
            emptyRange.Style.Font.Italic = true;
            ApplyTableRowStyle(emptyRange, false);
            row += 1;
        }
        else
        {
            var dataEndRow = row - 1;
            var ownerRange = sheet.Range(dataStartRow, 1, dataEndRow, 4);
            ownerRange.Style.Alignment.WrapText = true;
            sheet.Rows(dataStartRow, dataEndRow).AdjustToContents(18, 36);
            sheet.Columns(1, 4).AdjustToContents(14, 36);
        }

        return row;
    }

    private static void ResetOverviewArea(IXLWorksheet sheet, int startRow, int minRows, int maxColumns)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? startRow + minRows;
        var endRow = Math.Max(lastRow, startRow + minRows);
        var range = sheet.Range(startRow, 1, endRow, maxColumns);
        range.Unmerge();
        range.Clear(XLClearOptions.Contents);
    }

    private static void ApplyOverviewColumnWidths(IXLWorksheet sheet)
    {
        sheet.Column(1).Width = 16;
        sheet.Column(2).Width = 18;
        sheet.Column(3).Width = 18;
        sheet.Column(4).Width = 18;
        sheet.Column(5).Width = 16;
        sheet.Column(6).Width = 12;
    }

    private static void WriteKpiCard(IXLWorksheet sheet, int startRow, int startColumn, string label, object value)
    {
        var labelRange = sheet.Range(startRow, startColumn, startRow, startColumn + 2);
        labelRange.Merge();
        labelRange.Value = label;
        ApplyCardLabelStyle(labelRange);

        var valueRange = sheet.Range(startRow + 1, startColumn, startRow + 1, startColumn + 2);
        valueRange.Merge();
        var valueCell = valueRange.FirstCell();
        valueCell.Value = value switch
        {
            decimal decimalValue => decimalValue,
            int intValue => intValue,
            long longValue => longValue,
            double doubleValue => doubleValue,
            _ => value?.ToString() ?? string.Empty
        };
        ApplyCardValueStyle(valueRange);

        if (value is decimal or double)
        {
            valueRange.Style.NumberFormat.Format = CurrencyFormat;
            valueRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        else if (value is int or long)
        {
            valueRange.Style.NumberFormat.Format = IntegerFormat;
            valueRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        sheet.Row(startRow).Height = 18;
        sheet.Row(startRow + 1).Height = 22;
    }

    private static void ApplySummaryLayout(IXLWorksheet sheet, int rowCount)
    {
        sheet.SheetView.FreezeRows(7);
        sheet.Row(7).Height = 22;

        sheet.Column(1).Width = 12.4;
        sheet.Column(2).Width = 16;
        sheet.Column(3).Width = 30;
        sheet.Column(4).Width = 18;
        for (var col = 5; col <= 11; col++)
        {
            sheet.Column(col).Width = 16;
        }
        sheet.Column(12).Width = 10;
        sheet.Column(13).Width = 12;

        sheet.Column(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Column(3).Style.Alignment.WrapText = true;
        ApplyTableHeaderStyle(sheet.Range(7, 1, 7, 13));
        if (rowCount > 0)
        {
            var endRow = 8 + rowCount - 1;
            for (var row = 8; row <= endRow; row++)
            {
                ApplyTableRowStyle(sheet.Range(row, 1, row, 13), row % 2 == 0);
            }
            SetCurrencyFormat(sheet.Range(8, 5, endRow, 11));
            SetIntegerFormat(sheet.Range(8, 12, endRow, 12));
            sheet.Rows(8, endRow).AdjustToContents(28.5, 60);
            sheet.Columns(2, 4).AdjustToContents(14, 36);
        }
    }

    private static void ApplyDetailsLayout(IXLWorksheet sheet, int rowCount)
    {
        sheet.SheetView.FreezeRows(7);
        sheet.Row(7).Height = 22;

        sheet.Column(1).Width = 6;
        sheet.Column(2).Width = 12;
        sheet.Column(3).Width = 12;
        sheet.Column(4).Width = 10;
        sheet.Column(5).Width = 16;
        sheet.Column(6).Width = 16;
        sheet.Column(7).Width = 28;
        sheet.Column(8).Width = 18.5;
        sheet.Column(9).Width = 35;
        for (var col = 10; col <= 14; col++)
        {
            sheet.Column(col).Width = 16;
        }
        sheet.Column(15).Width = 16;
        sheet.Column(16).Width = 16;
        sheet.Column(17).Width = 14;

        sheet.Column(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Column(1).Style.NumberFormat.Format = IntegerFormat;
        sheet.Column(7).Style.Alignment.WrapText = true;
        sheet.Column(9).Style.Alignment.WrapText = true;

        var headers = new[]
        {
            "STT",
            "Ngày CT",
            "Kỳ áp dụng",
            "Loại",
            "MST Bên bán",
            "MST KH",
            "Tên KH",
            "Số chứng từ",
            "Diễn giải",
            "Doanh thu",
            "VAT",
            "Tăng nợ",
            "Giảm nợ",
            "Số dư lũy kế",
            "Người tạo",
            "Người duyệt",
            "Batch/Import",
        };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(7, i + 1).Value = headers[i];
        }

        ApplyTableHeaderStyle(sheet.Range(7, 1, 7, 17));
        if (rowCount > 0)
        {
            var endRow = 8 + rowCount - 1;
            for (var row = 8; row <= endRow; row++)
            {
                ApplyTableRowStyle(sheet.Range(row, 1, row, 17), row % 2 == 0);
            }
            SetCurrencyFormat(sheet.Range(8, 10, endRow, 14));
            sheet.Rows(8, endRow).AdjustToContents(28.5, 70);
            sheet.Columns(7, 9).AdjustToContents(14, 50);
        }

        ClampColumnWidths(sheet, 10, 50);
    }

    private static void ApplyAgingLayout(IXLWorksheet sheet, int rowCount)
    {
        sheet.SheetView.FreezeRows(7);
        sheet.Row(7).Height = 22;

        sheet.Column(1).Width = 6;
        sheet.Column(2).Width = 16;
        sheet.Column(3).Width = 30;
        sheet.Column(4).Width = 16;
        for (var col = 5; col <= 11; col++)
        {
            sheet.Column(col).Width = 16;
        }
        sheet.Column(12).Width = 12;

        sheet.Column(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Column(3).Style.Alignment.WrapText = true;
        ApplyTableHeaderStyle(sheet.Range(7, 1, 7, 12));
        if (rowCount > 0)
        {
            var endRow = 8 + rowCount - 1;
            for (var row = 8; row <= endRow; row++)
            {
                ApplyTableRowStyle(sheet.Range(row, 1, row, 12), row % 2 == 0);
            }
            SetCurrencyFormat(sheet.Range(8, 5, endRow, 11));
            sheet.Rows(8, endRow).AdjustToContents(28.5, 60);
            sheet.Columns(2, 3).AdjustToContents(14, 36);
        }

        ClampColumnWidths(sheet, 10, 50);
    }

    private static void ClearRows(IXLWorksheet sheet, int startRow)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? startRow;
        if (lastRow >= startRow)
        {
            sheet.Rows(startRow, lastRow).Clear(XLClearOptions.Contents);
        }
    }

    private static void CopyRowStyle(IXLWorksheet sheet, int sourceRow, int targetRow)
    {
        if (targetRow == sourceRow)
        {
            return;
        }

        sheet.Row(targetRow).Style = sheet.Row(sourceRow).Style;
    }
}
