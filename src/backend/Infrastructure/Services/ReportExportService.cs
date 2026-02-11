using ClosedXML.Excel;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Reports;
using Dapper;
using Microsoft.Extensions.Configuration;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReportExportService : IReportExportService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IReportService _reportService;
    private readonly ICurrentUser _currentUser;
    private readonly IConfiguration _configuration;

    public ReportExportService(
        IDbConnectionFactory connectionFactory,
        IReportService reportService,
        ICurrentUser currentUser,
        IConfiguration configuration)
    {
        _connectionFactory = connectionFactory;
        _reportService = reportService;
        _currentUser = currentUser;
        _configuration = configuration;
    }

    public async Task<ReportExportResult> ExportAsync(ReportExportRequest request, CancellationToken ct)
    {
        var from = request.From ?? new DateOnly(1900, 1, 1);
        var to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var asOf = request.AsOfDate ?? to;
        var exportKind = request.Kind;
        var templatePath = ResolveTemplatePath(exportKind);

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        var parameters = new
        {
            from,
            to,
            asOf,
            sellerTaxCode = request.SellerTaxCode,
            customerTaxCode = request.CustomerTaxCode,
            ownerId = request.OwnerId
        };
        var sellerName = await LoadSellerNameAsync(connection, request.SellerTaxCode, ct);

        using var workbook = new XLWorkbook(templatePath);
        string fileName;

        switch (exportKind)
        {
            case ReportExportKind.Overview:
            {
                var preferences = await LoadPreferencesAsync(ct);
                var kpis = await _reportService.GetKpisAsync(
                    new ReportKpiRequest(from, to, asOf, request.SellerTaxCode, request.CustomerTaxCode, request.OwnerId, preferences.DueSoonDays),
                    ct);
                var insights = await _reportService.GetInsightsAsync(
                    new ReportInsightsRequest(from, to, asOf, request.SellerTaxCode, request.CustomerTaxCode, request.OwnerId, 5),
                    ct);
                var overviewSheet = GetOrCreateSheet(workbook, "TongQuan");
                FillHeader(overviewSheet, request, sellerName, from, to, asOf);
                WriteOverview(overviewSheet, kpis, insights, preferences.DueSoonDays, from, to);
                KeepOnlySheets(workbook, "TongQuan");
                fileName = $"CongNo_TongQuan_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                break;
            }
            case ReportExportKind.Summary:
            {
                var summaryRows = (await connection.QueryAsync<ExportSummaryRow>(
                    new CommandDefinition(SummarySql, parameters, cancellationToken: ct))).ToList();
                var summarySheet = GetOrCreateSheet(workbook, "TongHop");
                FillHeader(summarySheet, request, sellerName, from, to, asOf);
                WriteSummary(summarySheet, summaryRows);
                KeepOnlySheets(workbook, "TongHop");
                fileName = $"CongNo_TongHop_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                break;
            }
            case ReportExportKind.Statement:
            {
                var summaryRows = (await connection.QueryAsync<ExportSummaryRow>(
                    new CommandDefinition(SummarySql, parameters, cancellationToken: ct))).ToList();
                var detailLines = (await connection.QueryAsync<ReportStatementLine>(
                    new CommandDefinition(DetailSql, parameters, cancellationToken: ct))).ToList();
                var detailWithRunning = ApplyRunningBalance(detailLines, summaryRows);
                var detailSheet = GetOrCreateSheet(workbook, "ChiTiet");
                FillHeader(detailSheet, request, sellerName, from, to, asOf);
                WriteDetails(detailSheet, detailWithRunning);
                KeepOnlySheets(workbook, "ChiTiet");
                fileName = $"CongNo_SaoKe_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                break;
            }
            case ReportExportKind.Aging:
            {
                var agingRows = await _reportService.GetAgingAsync(
                    new ReportAgingRequest(asOf, request.SellerTaxCode, request.CustomerTaxCode, request.OwnerId), ct);
                var agingSheet = GetOrCreateSheet(workbook, "Aging");
                FillHeader(agingSheet, request, sellerName, from, to, asOf);
                WriteAging(agingSheet, agingRows);
                KeepOnlySheets(workbook, "Aging");
                fileName = $"CongNo_TuoiNo_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                break;
            }
            default:
            {
                var summaryRows = (await connection.QueryAsync<ExportSummaryRow>(
                    new CommandDefinition(SummarySql, parameters, cancellationToken: ct))).ToList();
                var detailLines = (await connection.QueryAsync<ReportStatementLine>(
                    new CommandDefinition(DetailSql, parameters, cancellationToken: ct))).ToList();
                var agingRows = await _reportService.GetAgingAsync(
                    new ReportAgingRequest(asOf, request.SellerTaxCode, request.CustomerTaxCode, request.OwnerId), ct);
                var detailWithRunning = ApplyRunningBalance(detailLines, summaryRows);
                var preferences = await LoadPreferencesAsync(ct);
                var kpis = await _reportService.GetKpisAsync(
                    new ReportKpiRequest(from, to, asOf, request.SellerTaxCode, request.CustomerTaxCode, request.OwnerId, preferences.DueSoonDays),
                    ct);
                var insights = await _reportService.GetInsightsAsync(
                    new ReportInsightsRequest(from, to, asOf, request.SellerTaxCode, request.CustomerTaxCode, request.OwnerId, 5),
                    ct);

                var overviewSheet = GetOrCreateSheet(workbook, "TongQuan");
                FillHeader(overviewSheet, request, sellerName, from, to, asOf);
                FillHeader(GetOrCreateSheet(workbook, "TongHop"), request, sellerName, from, to, asOf);
                FillHeader(GetOrCreateSheet(workbook, "ChiTiet"), request, sellerName, from, to, asOf);
                FillHeader(GetOrCreateSheet(workbook, "Aging"), request, sellerName, from, to, asOf);

                WriteOverview(overviewSheet, kpis, insights, preferences.DueSoonDays, from, to);
                WriteSummary(workbook.Worksheet("TongHop"), summaryRows);
                WriteDetails(workbook.Worksheet("ChiTiet"), detailWithRunning);
                WriteAging(workbook.Worksheet("Aging"), agingRows);

                OrderSheets(workbook, "TongQuan", "TongHop", "ChiTiet", "Aging");
                SetActiveSheet(workbook, "TongQuan");
                fileName = $"CongNo_Report_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                break;
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new ReportExportResult(stream.ToArray(), fileName);
    }

    private async Task<ReportPreferencesDto> LoadPreferencesAsync(CancellationToken ct)
    {
        return _currentUser.UserId.HasValue
            ? await _reportService.GetPreferencesAsync(_currentUser.UserId.Value, ct)
            : new ReportPreferencesDto(Array.Empty<string>(), 7);
    }

    private static IXLWorksheet GetOrCreateSheet(XLWorkbook workbook, string name)
    {
        return workbook.Worksheets.FirstOrDefault(sheet => sheet.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.Add(name);
    }

    private static void KeepOnlySheets(XLWorkbook workbook, params string[] allowedNames)
    {
        var allowed = new HashSet<string>(allowedNames, StringComparer.OrdinalIgnoreCase);
        var toRemove = workbook.Worksheets
            .Where(sheet => !allowed.Contains(sheet.Name))
            .Select(sheet => sheet.Name)
            .ToList();
        foreach (var name in toRemove)
        {
            workbook.Worksheets.Delete(name);
        }
    }

    private static void OrderSheets(XLWorkbook workbook, params string[] orderedNames)
    {
        var normalized = new HashSet<string>(orderedNames, StringComparer.OrdinalIgnoreCase);
        var position = 1;

        foreach (var name in orderedNames)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (sheet is null)
            {
                continue;
            }

            sheet.Position = position++;
        }

        var remaining = workbook.Worksheets
            .Where(sheet => !normalized.Contains(sheet.Name))
            .OrderBy(sheet => sheet.Position)
            .ToList();

        foreach (var sheet in remaining)
        {
            sheet.Position = position++;
        }
    }

    private static void SetActiveSheet(XLWorkbook workbook, string name)
    {
        foreach (var sheet in workbook.Worksheets)
        {
            sheet.TabActive = false;
        }

        var target = workbook.Worksheets.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        target?.SetTabActive();
    }

    private string ResolveTemplatePath(ReportExportKind kind)
    {
        var configured = _configuration[$"Reports:TemplatePaths:{kind}"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = _configuration["Reports:TemplatePath"];
        }

        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = kind switch
            {
                ReportExportKind.Overview => "Templates/Mau_DoiSoat_CongNo_Golden_TongQuan.xlsx",
                ReportExportKind.Summary => "Templates/Mau_DoiSoat_CongNo_Golden_TongHop.xlsx",
                ReportExportKind.Statement => "Templates/Mau_DoiSoat_CongNo_Golden_ChiTiet.xlsx",
                ReportExportKind.Aging => "Templates/Mau_DoiSoat_CongNo_Golden_Aging.xlsx",
                _ => "Templates/Mau_DoiSoat_CongNo_Golden.xlsx"
            };
        }

        var path = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(AppContext.BaseDirectory, configured);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Report template not found.", path);
        }

        return path;
    }

    private static List<ReportStatementLine> ApplyRunningBalance(
        IReadOnlyList<ReportStatementLine> lines,
        IReadOnlyList<ExportSummaryRow> summaryRows)
    {
        var openingMap = summaryRows
            .GroupBy(s => s.CustomerTaxCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().OpeningBalance, StringComparer.OrdinalIgnoreCase);

        var grouped = lines
            .GroupBy(l => l.CustomerTaxCode, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.FirstOrDefault()?.CustomerName ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        var result = new List<ReportStatementLine>(lines.Count);
        foreach (var group in grouped)
        {
            var running = openingMap.TryGetValue(group.Key, out var opening) ? opening : 0m;

            var ordered = group
                .OrderBy(l => l.DocumentDate)
                .ThenBy(l => GetTypeOrder(l.Type))
                .ThenBy(l => l.DocumentNo)
                .ToList();

            foreach (var line in ordered)
            {
                running += line.Increase - line.Decrease;
                line.RunningBalance = running;
                result.Add(line);
            }
        }

        return result;
    }

    private static int GetTypeOrder(string type)
    {
        return type.ToUpperInvariant() switch
        {
            "INVOICE" => 1,
            "ADVANCE" => 2,
            "RECEIPT" => 3,
            _ => 9
        };
    }

    private async Task<string?> LoadSellerNameAsync(
        System.Data.Common.DbConnection connection,
        string? sellerTaxCode,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sellerTaxCode))
        {
            return null;
        }

        return await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(SellerNameSql, new { sellerTaxCode }, cancellationToken: ct));
    }
}
