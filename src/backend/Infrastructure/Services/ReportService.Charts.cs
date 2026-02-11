using CongNoGolden.Application.Reports;
using Dapper;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReportService
{
    private const string ReportCashFlowSql = @"
WITH days AS (
    SELECT generate_series(@from::date, @to::date, interval '1 day')::date AS day
),
receipt AS (
    SELECT r.receipt_date::date AS day,
           SUM(r.amount) AS receipted_total
    FROM congno.receipts r
    JOIN congno.customers c ON c.tax_code = r.customer_tax_code
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND r.receipt_date >= @from
      AND r.receipt_date <= @to
      AND (@sellerTaxCode IS NULL OR r.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR r.customer_tax_code = @customerTaxCode)
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    GROUP BY r.receipt_date::date
)
SELECT d.day AS date,
       COALESCE(r.receipted_total, 0) AS value
FROM days d
LEFT JOIN receipt r ON r.day = d.day
ORDER BY d.day;
";

    private const string ReportAgingDistributionSql = ReportAgingBaseCte + @"
SELECT
    COALESCE(SUM(CASE WHEN days_past_due <= 30 THEN outstanding ELSE 0 END), 0) AS bucket0to30,
    COALESCE(SUM(CASE WHEN days_past_due BETWEEN 31 AND 60 THEN outstanding ELSE 0 END), 0) AS bucket31to60,
    COALESCE(SUM(CASE WHEN days_past_due BETWEEN 61 AND 90 THEN outstanding ELSE 0 END), 0) AS bucket61to90,
    COALESCE(SUM(CASE WHEN days_past_due BETWEEN 91 AND 180 THEN outstanding ELSE 0 END), 0) AS bucket91to180,
    COALESCE(SUM(CASE WHEN days_past_due > 180 THEN outstanding ELSE 0 END), 0) AS bucketover180
FROM bucketed;
";

    private const string ReportAllocationStatusSql = @"
SELECT
    CASE
        WHEN r.allocation_status = 'ALLOCATED' THEN 'ALLOCATED'
        WHEN r.allocation_status = 'PARTIAL' THEN 'PARTIAL'
        ELSE 'UNALLOCATED'
    END AS status,
    COALESCE(SUM(r.amount), 0) AS amount
FROM congno.receipts r
JOIN congno.customers c ON c.tax_code = r.customer_tax_code
WHERE r.deleted_at IS NULL
  AND r.status = 'APPROVED'
  AND r.receipt_date >= @from
  AND r.receipt_date <= @to
  AND (@sellerTaxCode IS NULL OR r.seller_tax_code = @sellerTaxCode)
  AND (@customerTaxCode IS NULL OR r.customer_tax_code = @customerTaxCode)
  AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
GROUP BY 1;
";

    public async Task<ReportChartsDto> GetChartsAsync(ReportChartsRequest request, CancellationToken ct)
    {
        var from = request.From ?? new DateOnly(1900, 1, 1);
        var to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var asOf = request.AsOfDate ?? to;

        var parameters = new
        {
            from,
            to,
            asOf,
            sellerTaxCode = request.SellerTaxCode,
            customerTaxCode = request.CustomerTaxCode,
            ownerId = request.OwnerId
        };

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(
                $"{ReportCashFlowSql}\n{ReportAgingDistributionSql}\n{ReportAllocationStatusSql}",
                parameters,
                cancellationToken: ct));

        var cashFlow = (await multi.ReadAsync<ReportChartPointRow>()).ToList();
        var aging = await multi.ReadSingleAsync<ReportAgingDistributionRow>();
        var allocation = (await multi.ReadAsync<ReportAllocationStatusRow>()).ToList();

        return new ReportChartsDto(
            cashFlow.Select(p => new ReportChartPointDto(p.Date, p.Value)).ToList(),
            new ReportAgingDistributionDto(
                aging.Bucket0To30,
                aging.Bucket31To60,
                aging.Bucket61To90,
                aging.Bucket91To180,
                aging.BucketOver180),
            allocation.Select(a => new ReportAllocationStatusDto(a.Status, a.Amount)).ToList());
    }

    private sealed class ReportChartPointRow
    {
        public DateOnly Date { get; init; }
        public decimal Value { get; init; }
    }

    private sealed class ReportAgingDistributionRow
    {
        public decimal Bucket0To30 { get; init; }
        public decimal Bucket31To60 { get; init; }
        public decimal Bucket61To90 { get; init; }
        public decimal Bucket91To180 { get; init; }
        public decimal BucketOver180 { get; init; }
    }

    private sealed class ReportAllocationStatusRow
    {
        public string Status { get; init; } = string.Empty;
        public decimal Amount { get; init; }
    }
}
