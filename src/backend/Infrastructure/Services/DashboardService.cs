using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Dashboard;
using CongNoGolden.Infrastructure.Services.Common;
using Dapper;
using System.Globalization;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class DashboardService : IDashboardService
{
    private const decimal OnTimeThreshold = 0.95m;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ICurrentUser _currentUser;

    public DashboardService(IDbConnectionFactory connectionFactory, ICurrentUser currentUser)
    {
        _connectionFactory = connectionFactory;
        _currentUser = currentUser;
    }

    public async Task<DashboardOverviewDto> GetOverviewAsync(DashboardOverviewRequest request, CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var canViewImports = _currentUser.HasAnyRole("Admin", "Supervisor");
        var canViewLocks = canViewImports;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var to = request.To ?? today;
        var months = request.Months.GetValueOrDefault(6);
        if (months < 1)
        {
            months = 1;
        }
        else if (months > 24)
        {
            months = 24;
        }

        var from = request.From ?? StartOfMonth(to).AddMonths(-(months - 1));
        if (from > to)
        {
            from = StartOfMonth(to);
        }
        var previousFrom = from.AddMonths(-1);
        var previousTo = to.AddMonths(-1);

        var hasExplicitTrend = !string.IsNullOrWhiteSpace(request.TrendGranularity) || request.TrendPeriods.HasValue;
        var trendGranularity = NormalizeTrendGranularity(request.TrendGranularity);
        var trendPeriods = NormalizeTrendPeriods(
            trendGranularity,
            request.TrendPeriods ?? (trendGranularity == "week" ? 4 : request.Months));

        var trendAnchor = request.To ?? today;
        var trendSeriesTo = trendGranularity == "week"
            ? StartOfWeek(trendAnchor)
            : StartOfMonth(trendAnchor);
        var trendSeriesFrom = trendGranularity == "week"
            ? trendSeriesTo.AddDays(-7 * (trendPeriods - 1))
            : StartOfMonth(trendSeriesTo).AddMonths(-(trendPeriods - 1));

        var trendFilterFrom = trendSeriesFrom;
        var trendFilterTo = trendGranularity == "week"
            ? trendSeriesTo.AddDays(6)
            : EndOfMonth(trendSeriesTo);

        if (!hasExplicitTrend)
        {
            trendSeriesFrom = from;
            trendSeriesTo = to;
            trendFilterFrom = from;
            trendFilterTo = to;
        }

        var top = request.Top.GetValueOrDefault(5);
        if (top < 3)
        {
            top = 3;
        }
        else if (top > 20)
        {
            top = 20;
        }

        var ownerId = _currentUser.ResolveOwnerFilter();
        var parameters = new
        {
            ownerId,
            from,
            to,
            asOf = to,
            top,
            onTimeThreshold = OnTimeThreshold,
            trendSeriesFrom,
            trendSeriesTo,
            trendFilterFrom,
            trendFilterTo,
            trendGranularity
        };
        var previousParameters = new
        {
            ownerId,
            from = previousFrom,
            to = previousTo,
            asOf = previousTo,
            top,
            onTimeThreshold = OnTimeThreshold,
            trendSeriesFrom,
            trendSeriesTo,
            trendFilterFrom,
            trendFilterTo,
            trendGranularity
        };

        await using var connection = _connectionFactory.CreateRead();
        await connection.OpenAsync(ct);

        var kpiSnapshot = await connection.QuerySingleAsync<DashboardKpiRow>(
            new CommandDefinition(DashboardKpiSql, parameters, cancellationToken: ct));

        var onTimeCustomers = await connection.QuerySingleAsync<int>(
            new CommandDefinition(DashboardOnTimeCountSql, parameters, cancellationToken: ct));
        var previousKpiSnapshot = await connection.QuerySingleAsync<DashboardKpiRow>(
            new CommandDefinition(DashboardKpiSql, previousParameters, cancellationToken: ct));
        var previousOnTimeCustomers = await connection.QuerySingleAsync<int>(
            new CommandDefinition(DashboardOnTimeCountSql, previousParameters, cancellationToken: ct));

        var kpis = new DashboardKpiDto(
            kpiSnapshot.OutstandingInvoice + kpiSnapshot.OutstandingAdvance,
            kpiSnapshot.OutstandingInvoice,
            kpiSnapshot.OutstandingAdvance,
            kpiSnapshot.OverdueTotal,
            kpiSnapshot.OverdueCustomers,
            onTimeCustomers,
            kpiSnapshot.UnallocatedReceiptsAmount,
            kpiSnapshot.UnallocatedReceiptsCount,
            kpiSnapshot.PendingReceiptsCount,
            kpiSnapshot.PendingReceiptsAmount,
            kpiSnapshot.PendingAdvancesCount,
            kpiSnapshot.PendingAdvancesAmount,
            canViewImports ? kpiSnapshot.PendingImportBatches : 0,
            canViewLocks ? kpiSnapshot.PeriodLocksCount : 0);
        var kpiMoM = BuildKpiMoM(kpiSnapshot, onTimeCustomers, previousKpiSnapshot, previousOnTimeCustomers);
        var executiveSummary = BuildExecutiveSummary(kpis, kpiMoM);

        var trendRows = (await connection.QueryAsync<DashboardTrendRow>(
            new CommandDefinition(DashboardTrendSql, parameters, cancellationToken: ct))).ToList();

        var trend = trendRows
            .Select(r => new DashboardTrendPoint(
                r.Period ?? string.Empty,
                r.InvoicedTotal,
                r.AdvancedTotal,
                r.ReceiptedTotal,
                r.InvoicedTotal + r.AdvancedTotal,
                r.ReceiptedTotal,
                r.ReceiptedTotal - (r.InvoicedTotal + r.AdvancedTotal)))
            .ToList();
        var cashflowForecast = BuildCashflowForecast(trend, trendGranularity, trendSeriesTo);

        var topOutstanding = (await connection.QueryAsync<DashboardTopRow>(
            new CommandDefinition(DashboardTopOutstandingSql, parameters, cancellationToken: ct)))
            .Select(MapTopItem)
            .ToList();

        var topOnTime = (await connection.QueryAsync<DashboardTopRow>(
            new CommandDefinition(DashboardTopOnTimeSql, parameters, cancellationToken: ct)))
            .Select(MapTopItem)
            .ToList();

        var topOverdueDays = (await connection.QueryAsync<DashboardTopRow>(
            new CommandDefinition(DashboardTopOverdueDaysSql, parameters, cancellationToken: ct)))
            .Select(MapTopItem)
            .ToList();

        var agingBuckets = (await connection.QueryAsync<DashboardAgingBucketRow>(
            new CommandDefinition(DashboardAgingBucketsSql, parameters, cancellationToken: ct)))
            .Select(r => new DashboardAgingBucketDto(
                r.Bucket ?? string.Empty,
                r.Amount))
            .ToList();

        var allocationStatuses = (await connection.QueryAsync<DashboardAllocationStatusRow>(
            new CommandDefinition(DashboardAllocationStatusSql, parameters, cancellationToken: ct)))
            .Select(r => new DashboardAllocationStatusDto(
                r.Status ?? string.Empty,
                r.Amount))
            .ToList();

        var lastUpdated = await connection.QuerySingleAsync<DateTime>(
            new CommandDefinition(DashboardLastUpdatedSql, cancellationToken: ct));

        return new DashboardOverviewDto(
            trendFilterFrom,
            trendFilterTo,
            executiveSummary,
            kpis,
            kpiMoM,
            trend,
            cashflowForecast,
            topOutstanding,
            topOnTime,
            topOverdueDays,
            agingBuckets,
            allocationStatuses,
            lastUpdated);
    }

    public async Task<IReadOnlyList<DashboardOverdueGroupItem>> GetOverdueGroupsAsync(
        DashboardOverdueGroupRequest request,
        CancellationToken ct)
    {
        _currentUser.EnsureUser();
        var ownerId = _currentUser.ResolveOwnerFilter();
        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var top = request.Top.GetValueOrDefault(5);
        if (top < 3)
        {
            top = 3;
        }
        else if (top > 20)
        {
            top = 20;
        }

        var groupBy = (request.GroupBy ?? "owner").Trim().ToLowerInvariant();
        var sql = groupBy switch
        {
            "customer" => DashboardOverdueByCustomerSql,
            "owner" => DashboardOverdueByOwnerSql,
            _ => DashboardOverdueByOwnerSql
        };

        var parameters = new
        {
            ownerId,
            asOf,
            top
        };

        await using var connection = _connectionFactory.CreateRead();
        await connection.OpenAsync(ct);

        var rows = await connection.QueryAsync<DashboardOverdueGroupRow>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        return rows
            .Select(r => new DashboardOverdueGroupItem(
                r.GroupKey ?? string.Empty,
                r.GroupName ?? r.GroupKey ?? string.Empty,
                r.TotalOutstanding,
                r.OverdueAmount,
                r.OverdueRatio,
                r.OverdueCustomers))
            .ToList();
    }

    private static DateOnly StartOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private static DateOnly EndOfMonth(DateOnly date)
    {
        var start = StartOfMonth(date);
        return start.AddMonths(1).AddDays(-1);
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var dayOfWeek = date.DayOfWeek;
        var delta = dayOfWeek == DayOfWeek.Sunday ? 6 : (int)dayOfWeek - 1;
        return date.AddDays(-delta);
    }

    private static string NormalizeTrendGranularity(string? value)
    {
        var normalized = (value ?? "month").Trim().ToLowerInvariant();
        return normalized == "week" ? "week" : "month";
    }

    private static int NormalizeTrendPeriods(string granularity, int? value)
    {
        var fallback = granularity == "week" ? 4 : 6;
        var periods = value ?? fallback;
        if (granularity == "week")
        {
            return Math.Clamp(periods, 2, 52);
        }

        return Math.Clamp(periods, 1, 24);
    }

    private static DashboardTopItem MapTopItem(DashboardTopRow row)
    {
        var name = string.IsNullOrWhiteSpace(row.CustomerName)
            ? row.CustomerTaxCode ?? string.Empty
            : row.CustomerName;

        return new DashboardTopItem(
            row.CustomerTaxCode ?? string.Empty,
            name,
            row.Amount,
            row.DaysPastDue);
    }

    private static IReadOnlyList<DashboardCashflowForecastPoint> BuildCashflowForecast(
        IReadOnlyList<DashboardTrendPoint> trend,
        string trendGranularity,
        DateOnly trendSeriesTo)
    {
        if (trend.Count == 0)
        {
            return Array.Empty<DashboardCashflowForecastPoint>();
        }

        var windowSize = Math.Min(4, trend.Count);
        var tail = trend.Skip(trend.Count - windowSize);
        var expectedAvg = tail.Average(point => point.ExpectedTotal);
        var actualAvg = tail.Average(point => point.ActualTotal);

        var horizon = trendGranularity == "week" ? 4 : 3;
        var anchor = trendGranularity == "week"
            ? StartOfWeek(trendSeriesTo)
            : StartOfMonth(trendSeriesTo);
        var result = new List<DashboardCashflowForecastPoint>(horizon);

        for (var i = 1; i <= horizon; i++)
        {
            var periodStart = trendGranularity == "week"
                ? anchor.AddDays(i * 7)
                : StartOfMonth(anchor).AddMonths(i);

            result.Add(new DashboardCashflowForecastPoint(
                FormatPeriodKey(periodStart, trendGranularity),
                expectedAvg,
                actualAvg,
                actualAvg - expectedAvg));
        }

        return result;
    }

    private static string FormatPeriodKey(DateOnly periodStart, string trendGranularity)
    {
        if (trendGranularity == "week")
        {
            var date = periodStart.ToDateTime(TimeOnly.MinValue);
            var isoYear = ISOWeek.GetYear(date);
            var isoWeek = ISOWeek.GetWeekOfYear(date);
            return $"{isoYear}-W{isoWeek:D2}";
        }

        return $"{periodStart.Year:D4}-{periodStart.Month:D2}";
    }

    private static DashboardKpiMoMDto BuildKpiMoM(
        DashboardKpiRow current,
        int currentOnTimeCustomers,
        DashboardKpiRow previous,
        int previousOnTimeCustomers)
    {
        var currentTotalOutstanding = current.OutstandingInvoice + current.OutstandingAdvance;
        var previousTotalOutstanding = previous.OutstandingInvoice + previous.OutstandingAdvance;

        return new DashboardKpiMoMDto(
            CreateDelta(currentTotalOutstanding, previousTotalOutstanding),
            CreateDelta(current.OutstandingInvoice, previous.OutstandingInvoice),
            CreateDelta(current.OutstandingAdvance, previous.OutstandingAdvance),
            CreateDelta(current.OverdueTotal, previous.OverdueTotal),
            CreateDelta(current.UnallocatedReceiptsAmount, previous.UnallocatedReceiptsAmount),
            CreateDelta(currentOnTimeCustomers, previousOnTimeCustomers));
    }

    private static DashboardExecutiveSummaryDto BuildExecutiveSummary(
        DashboardKpiDto kpis,
        DashboardKpiMoMDto kpiMoM)
    {
        var status = "stable";
        var actionHint = "Theo dõi dashboard mỗi ngày để phát hiện biến động công nợ sớm.";
        var movement = DescribeDelta(kpiMoM.TotalOutstanding.Delta);
        var message = $"Tổng dư công nợ đang {movement} so với tháng trước.";

        if (kpis.OverdueTotal > 0)
        {
            status = "critical";
            actionHint = "Ưu tiên xử lý danh sách quá hạn lâu nhất và xác nhận lịch thu trong 24-48h.";
            message =
                $"Đang có {kpis.OverdueCustomers} khách hàng quá hạn với tổng giá trị {kpis.OverdueTotal:N0} đ.";
        }
        else if (kpis.UnallocatedReceiptsAmount > 0)
        {
            status = "warning";
            actionHint = "Phân bổ các phiếu thu treo để giảm lệch công nợ.";
            message =
                $"Có {kpis.UnallocatedReceiptsCount} phiếu thu chưa phân bổ ({kpis.UnallocatedReceiptsAmount:N0} đ).";
        }
        else if (kpis.TotalOutstanding <= 0)
        {
            status = "good";
            actionHint = "Duy trì kiểm soát kỳ hạn thanh toán và theo dõi nhóm khách hàng mới phát sinh.";
            message = "Không còn dư công nợ mở tại kỳ hiện tại.";
        }

        return new DashboardExecutiveSummaryDto(
            status,
            message,
            actionHint,
            DateTime.UtcNow);
    }

    private static DashboardKpiDeltaDto CreateDelta(decimal current, decimal previous)
    {
        var delta = current - previous;
        decimal? percent = null;
        if (previous != 0)
        {
            percent = decimal.Round((delta / previous) * 100m, 2, MidpointRounding.AwayFromZero);
        }

        return new DashboardKpiDeltaDto(current, previous, delta, percent);
    }

    private static DashboardKpiDeltaDto CreateDelta(int current, int previous) =>
        CreateDelta((decimal)current, (decimal)previous);

    private static string DescribeDelta(decimal value)
    {
        if (value > 0)
        {
            return "tăng";
        }

        if (value < 0)
        {
            return "giảm";
        }

        return "đi ngang";
    }

    private sealed class DashboardKpiRow
    {
        public decimal OutstandingInvoice { get; set; }
        public decimal OutstandingAdvance { get; set; }
        public decimal OverdueTotal { get; set; }
        public int OverdueCustomers { get; set; }
        public decimal UnallocatedReceiptsAmount { get; set; }
        public int UnallocatedReceiptsCount { get; set; }
        public int PendingReceiptsCount { get; set; }
        public decimal PendingReceiptsAmount { get; set; }
        public int PendingAdvancesCount { get; set; }
        public decimal PendingAdvancesAmount { get; set; }
        public int PendingImportBatches { get; set; }
        public int PeriodLocksCount { get; set; }
    }

    private sealed class DashboardTrendRow
    {
        public string? Period { get; set; }
        public decimal InvoicedTotal { get; set; }
        public decimal AdvancedTotal { get; set; }
        public decimal ReceiptedTotal { get; set; }
    }

    private sealed class DashboardTopRow
    {
        public string? CustomerTaxCode { get; set; }
        public string? CustomerName { get; set; }
        public decimal Amount { get; set; }
        public int? DaysPastDue { get; set; }
    }

    private sealed class DashboardAgingBucketRow
    {
        public string? Bucket { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class DashboardAllocationStatusRow
    {
        public string? Status { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class DashboardOverdueGroupRow
    {
        public string? GroupKey { get; set; }
        public string? GroupName { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal OverdueAmount { get; set; }
        public decimal OverdueRatio { get; set; }
        public int OverdueCustomers { get; set; }
    }
}
