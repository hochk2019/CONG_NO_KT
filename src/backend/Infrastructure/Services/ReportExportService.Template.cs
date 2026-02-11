using ClosedXML.Excel;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReportExportService
{
    private static readonly XLColor PrimaryColor = XLColor.FromHtml("#1E3A5F");
    private static readonly XLColor PrimaryTextColor = XLColor.White;
    private static readonly XLColor SectionFill = XLColor.FromHtml("#E8EEF5");
    private static readonly XLColor CardFill = XLColor.FromHtml("#F3F6FA");
    private static readonly XLColor ZebraFill = XLColor.FromHtml("#F8FAFC");
    private static readonly XLColor BorderColor = XLColor.FromHtml("#CBD5E1");

    private const string CurrencyFormat = "#,##0 \"đ\"";
    private const string IntegerFormat = "0";

    private static void ApplyTitleStyle(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 16;
        range.Style.Font.FontColor = PrimaryTextColor;
        range.Style.Fill.BackgroundColor = PrimaryColor;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void ApplySubtitleStyle(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 11;
        range.Style.Fill.BackgroundColor = SectionFill;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void ApplySectionTitleStyle(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 12;
        range.Style.Fill.BackgroundColor = SectionFill;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void ApplyTableHeaderStyle(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = PrimaryTextColor;
        range.Style.Fill.BackgroundColor = PrimaryColor;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ApplyBorder(range);
    }

    private static void ApplyTableRowStyle(IXLRange range, bool zebra)
    {
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        if (zebra)
        {
            range.Style.Fill.BackgroundColor = ZebraFill;
        }

        ApplyBorder(range);
    }

    private static void ApplyCardLabelStyle(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 10;
        range.Style.Fill.BackgroundColor = CardFill;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ApplyBorder(range);
    }

    private static void ApplyCardValueStyle(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 13;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ApplyBorder(range);
    }

    private static void ApplyBorder(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = BorderColor;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorderColor = BorderColor;
    }

    private static void SetCurrencyFormat(IXLRange range)
    {
        range.Style.NumberFormat.Format = CurrencyFormat;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static void SetIntegerFormat(IXLRange range)
    {
        range.Style.NumberFormat.Format = IntegerFormat;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static void ClampColumnWidths(IXLWorksheet sheet, double minWidth, double maxWidth)
    {
        foreach (var column in sheet.ColumnsUsed())
        {
            var width = column.Width;
            if (width < minWidth)
            {
                column.Width = minWidth;
                continue;
            }

            if (width > maxWidth)
            {
                column.Width = maxWidth;
            }
        }
    }

    private static void ApplyHeaderAreaLayout(IXLWorksheet sheet)
    {
        sheet.Rows(1, 6).Height = 18;
        sheet.Rows(1, 6).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Rows(1, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Rows(1, 6).Style.Alignment.WrapText = true;
        sheet.Rows(1, 6).Style.Font.FontSize = 10;
        sheet.Rows(1, 6).AdjustToContents(18, 36);

        if (sheet.Name.Equals("TongHop", StringComparison.OrdinalIgnoreCase)
            || sheet.Name.Equals("ChiTiet", StringComparison.OrdinalIgnoreCase)
            || sheet.Name.Equals("Aging", StringComparison.OrdinalIgnoreCase))
        {
            sheet.Rows(2, 6).Height = 25.5;
            sheet.Row(4).Hide();
        }

        var labelFill = XLColor.FromHtml("#F1F5F9");
        var valueFill = XLColor.White;

        var labelCells = new[]
        {
            "A2", "E2",
            "A3", "C3", "E3",
            "A4", "A5", "E5",
            "A6",
        };

        foreach (var address in labelCells)
        {
            var cell = sheet.Cell(address);
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = labelFill;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var valueCells = new[] { "B2", "F2", "B3", "D3", "F3", "B4", "B5", "F5", "B6" };
        foreach (var address in valueCells)
        {
            var cell = sheet.Cell(address);
            cell.Style.Fill.BackgroundColor = valueFill;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        MergeRange(sheet, 2, 2, 2, 4);
        MergeRange(sheet, 2, 6, 2, 8);
        MergeRange(sheet, 3, 6, 3, 8);
        MergeRange(sheet, 4, 2, 4, 8);
        MergeRange(sheet, 5, 2, 5, 4);
        MergeRange(sheet, 5, 6, 5, 8);
        MergeRange(sheet, 6, 2, 6, 4);

        if (sheet.Name.Equals("Aging", StringComparison.OrdinalIgnoreCase))
        {
            var label = sheet.Cell("A6");
            label.Style.Font.Bold = true;
            label.Style.Fill.BackgroundColor = labelFill;
            label.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var value = sheet.Cell("B6");
            value.Style.Fill.BackgroundColor = valueFill;
            value.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
    }

    private static void ApplyReportTitleLayout(IXLWorksheet sheet)
    {
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 6;
        var titleRange = sheet.Range(1, 1, 1, lastColumn);
        titleRange.Merge();
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 14;
        sheet.Row(1).Height = 24;
    }

    private static void MergeRange(IXLWorksheet sheet, int startRow, int startColumn, int endRow, int endColumn)
    {
        var range = sheet.Range(startRow, startColumn, endRow, endColumn);
        if (range.IsMerged())
        {
            range.Unmerge();
        }
        range.Merge();
    }
}
