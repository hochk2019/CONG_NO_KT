using CongNoGolden.Application.Reports;
using Dapper;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReportService
{
    private const string ReportTopOutstandingSql = ReportAgingBaseCte + @"
SELECT customer_tax_code AS customerTaxCode,
       customer_name AS customerName,
       total_outstanding AS amount,
       max_days_past_due AS daysPastDue
FROM agg
ORDER BY total_outstanding DESC
LIMIT @top;
";

    private const string ReportTopOnTimeSql = ReportOnTimeBaseCte + @"
SELECT customer_tax_code AS customerTaxCode,
       customer_name AS customerName,
       total_due AS amount,
       max_days_past_due AS daysPastDue,
       COALESCE((total_due - outstanding_due) / NULLIF(total_due, 0), 0) AS ratio
FROM agg
WHERE total_due > 0
  AND outstanding_due <= total_due * @onTimeThreshold
ORDER BY ratio DESC, total_due DESC, max_days_past_due ASC
LIMIT @top;
";

    private const string ReportOverdueByOwnerSql = ReportAgingBaseCte + @"
, owner_bucket AS (
    SELECT COALESCE(bucketed.owner_id, '00000000-0000-0000-0000-000000000000'::uuid) AS owner_id,
           COALESCE(u.full_name, u.username, 'Unknown') AS owner_name,
           bucketed.customer_tax_code,
           bucketed.outstanding,
           bucketed.days_past_due
    FROM bucketed
    LEFT JOIN congno.users u ON u.id = bucketed.owner_id
)
SELECT CASE WHEN owner_id = '00000000-0000-0000-0000-000000000000'::uuid THEN 'UNASSIGNED' ELSE owner_id::text END AS groupKey,
       CASE WHEN owner_id = '00000000-0000-0000-0000-000000000000'::uuid THEN 'Unassigned' ELSE owner_name END AS groupName,
       SUM(outstanding) AS totalOutstanding,
       SUM(CASE WHEN days_past_due > 0 THEN outstanding ELSE 0 END) AS overdueAmount,
       COALESCE(SUM(CASE WHEN days_past_due > 0 THEN outstanding ELSE 0 END) / NULLIF(SUM(outstanding), 0), 0) AS overdueRatio,
       COUNT(DISTINCT CASE WHEN days_past_due > 0 THEN customer_tax_code END) AS overdueCustomers
FROM owner_bucket
GROUP BY owner_id, owner_name
ORDER BY overdueAmount DESC, totalOutstanding DESC
LIMIT @top;
";

    public async Task<ReportInsightsDto> GetInsightsAsync(ReportInsightsRequest request, CancellationToken ct)
    {
        var from = request.From ?? new DateOnly(1900, 1, 1);
        var to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var asOf = request.AsOfDate ?? to;
        var top = request.Top <= 0 ? 5 : request.Top;

        var parameters = new
        {
            from,
            to,
            asOf,
            top,
            sellerTaxCode = request.SellerTaxCode,
            customerTaxCode = request.CustomerTaxCode,
            ownerId = request.OwnerId,
            onTimeThreshold = 0.95m
        };

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(
                $"{ReportTopOutstandingSql}\n{ReportTopOnTimeSql}\n{ReportOverdueByOwnerSql}",
                parameters,
                cancellationToken: ct));

        var topOutstanding = (await multi.ReadAsync<TopCustomerRow>())
            .Select(row => new ReportTopCustomerDto(
                row.CustomerTaxCode,
                row.CustomerName,
                row.Amount,
                row.DaysPastDue,
                row.Ratio))
            .ToList();

        var topOnTime = (await multi.ReadAsync<TopCustomerRow>())
            .Select(row => new ReportTopCustomerDto(
                row.CustomerTaxCode,
                row.CustomerName,
                row.Amount,
                row.DaysPastDue,
                row.Ratio))
            .ToList();

        var overdueByOwner = (await multi.ReadAsync<OverdueGroupRow>())
            .Select(row => new ReportOverdueGroupDto(
                row.GroupKey,
                row.GroupName,
                row.TotalOutstanding,
                row.OverdueAmount,
                row.OverdueRatio,
                row.OverdueCustomers))
            .ToList();

        return new ReportInsightsDto(topOutstanding, topOnTime, overdueByOwner);
    }

    private sealed class TopCustomerRow
    {
        public string CustomerTaxCode { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public int? DaysPastDue { get; init; }
        public decimal? Ratio { get; init; }
    }

    private sealed class OverdueGroupRow
    {
        public string GroupKey { get; init; } = string.Empty;
        public string GroupName { get; init; } = string.Empty;
        public decimal TotalOutstanding { get; init; }
        public decimal OverdueAmount { get; init; }
        public decimal OverdueRatio { get; init; }
        public int OverdueCustomers { get; init; }
    }
}
