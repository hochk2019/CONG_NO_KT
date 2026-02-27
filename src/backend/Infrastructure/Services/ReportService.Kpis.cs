using CongNoGolden.Application.Reports;
using Dapper;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReportService
{
    private const string ReportKpiSql = ReportOnTimeBaseCte + @"
, period_outstanding AS (
    SELECT
        COALESCE((SELECT SUM(outstanding) FROM invoice_period), 0) AS outstanding_invoice,
        COALESCE((SELECT SUM(outstanding) FROM advance_period), 0) AS outstanding_advance
),
unallocated_receipts AS (
    SELECT COALESCE(SUM(r.unallocated_amount), 0) AS amount,
           COALESCE(COUNT(*), 0) AS count
    FROM congno.receipts r
    JOIN congno.customers c ON c.tax_code = r.customer_tax_code
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND r.unallocated_amount > 0
      AND r.receipt_date >= @from
      AND r.receipt_date <= @to
      AND (@sellerTaxCode IS NULL OR r.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR r.customer_tax_code = @customerTaxCode)
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
),
overdue AS (
    SELECT
        COALESCE(SUM(outstanding), 0) AS overdue_amount,
        COUNT(DISTINCT customer_tax_code) AS overdue_customers
    FROM combined
    WHERE outstanding > 0
      AND due_date < @asOf
),
due_soon AS (
    SELECT
        COALESCE(SUM(outstanding), 0) AS due_soon_amount,
        COUNT(DISTINCT customer_tax_code) AS due_soon_customers
    FROM combined
    WHERE outstanding > 0
      AND due_date >= @asOf
      AND due_date <= @dueSoonDate
),
on_time AS (
    SELECT COUNT(*) AS on_time_customers
    FROM agg
    WHERE total_due > 0
      AND outstanding_due <= total_due * @onTimeThreshold
)
SELECT
    (SELECT outstanding_invoice FROM period_outstanding) AS outstandingInvoice,
    (SELECT outstanding_advance FROM period_outstanding) AS outstandingAdvance,
    (SELECT amount FROM unallocated_receipts) AS unallocatedReceiptsAmount,
    (SELECT count FROM unallocated_receipts) AS unallocatedReceiptsCount,
    (SELECT overdue_amount FROM overdue) AS overdueAmount,
    (SELECT overdue_customers FROM overdue) AS overdueCustomers,
    (SELECT due_soon_amount FROM due_soon) AS dueSoonAmount,
    (SELECT due_soon_customers FROM due_soon) AS dueSoonCustomers,
    (SELECT on_time_customers FROM on_time) AS onTimeCustomers
;";

    public async Task<ReportKpiDto> GetKpisAsync(ReportKpiRequest request, CancellationToken ct)
    {
        var from = request.From ?? new DateOnly(1900, 1, 1);
        var to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var asOf = request.AsOfDate ?? to;
        var dueSoonDays = Math.Clamp(request.DueSoonDays, 1, 10);
        var dueSoonDate = asOf.AddDays(dueSoonDays);

        var parameters = new
        {
            from,
            to,
            asOf,
            dueSoonDate,
            sellerTaxCode = request.SellerTaxCode,
            customerTaxCode = request.CustomerTaxCode,
            ownerId = request.OwnerId,
            onTimeThreshold = 0.95m
        };

        await using var connection = _connectionFactory.CreateRead();
        await connection.OpenAsync(ct);

        var row = await connection.QuerySingleAsync<ReportKpiRow>(
            new CommandDefinition(ReportKpiSql, parameters, cancellationToken: ct));

        var totalOutstanding = row.OutstandingInvoice + row.OutstandingAdvance;

        return new ReportKpiDto(
            totalOutstanding,
            row.OutstandingInvoice,
            row.OutstandingAdvance,
            row.UnallocatedReceiptsAmount,
            row.UnallocatedReceiptsCount,
            row.OverdueAmount,
            row.OverdueCustomers,
            row.DueSoonAmount,
            row.DueSoonCustomers,
            row.OnTimeCustomers);
    }

    private sealed class ReportKpiRow
    {
        public decimal OutstandingInvoice { get; init; }
        public decimal OutstandingAdvance { get; init; }
        public decimal UnallocatedReceiptsAmount { get; init; }
        public int UnallocatedReceiptsCount { get; init; }
        public decimal OverdueAmount { get; init; }
        public int OverdueCustomers { get; init; }
        public decimal DueSoonAmount { get; init; }
        public int DueSoonCustomers { get; init; }
        public int OnTimeCustomers { get; init; }
    }
}
