namespace CongNoGolden.Infrastructure.Services;

public sealed partial class DashboardService
{
    private const string AgingBaseCte = @"
WITH invoice_alloc AS (
    SELECT ra.invoice_id, SUM(ra.amount) AS allocated
    FROM congno.receipt_allocations ra
    JOIN congno.receipts r ON r.id = ra.receipt_id
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND r.receipt_date <= @asOf
    GROUP BY ra.invoice_id
),
advance_alloc AS (
    SELECT ra.advance_id, SUM(ra.amount) AS allocated
    FROM congno.receipt_allocations ra
    JOIN congno.receipts r ON r.id = ra.receipt_id
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND r.receipt_date <= @asOf
    GROUP BY ra.advance_id
),
invoice_out AS (
    SELECT i.customer_tax_code,
           c.name AS customer_name,
           c.accountant_owner_id AS owner_id,
           (i.total_amount - COALESCE(a.allocated, 0)) AS outstanding,
           (i.issue_date + (COALESCE(c.payment_terms_days, 0) || ' days')::interval)::date AS due_date
    FROM congno.invoices i
    JOIN congno.customers c ON c.tax_code = i.customer_tax_code
    LEFT JOIN invoice_alloc a ON a.invoice_id = i.id
    WHERE i.deleted_at IS NULL
      AND i.status <> 'VOID'
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
),
advance_out AS (
    SELECT a.customer_tax_code,
           c.name AS customer_name,
           c.accountant_owner_id AS owner_id,
           (a.amount - COALESCE(alloc.allocated, 0)) AS outstanding,
           (a.advance_date + (COALESCE(c.payment_terms_days, 0) || ' days')::interval)::date AS due_date
    FROM congno.advances a
    JOIN congno.customers c ON c.tax_code = a.customer_tax_code
    LEFT JOIN advance_alloc alloc ON alloc.advance_id = a.id
    WHERE a.deleted_at IS NULL
      AND a.status IN ('APPROVED','PAID')
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
),
combined AS (
    SELECT customer_tax_code, customer_name, owner_id, outstanding, due_date FROM invoice_out
    UNION ALL
    SELECT customer_tax_code, customer_name, owner_id, outstanding, due_date FROM advance_out
),
bucketed AS (
    SELECT customer_tax_code,
           customer_name,
           owner_id,
           outstanding,
           GREATEST(0, (@asOf::date - due_date))::int AS days_past_due
    FROM combined
    WHERE outstanding > 0
),
agg AS (
    SELECT customer_tax_code,
           customer_name,
           SUM(outstanding) AS total_outstanding,
           SUM(CASE WHEN days_past_due > 0 THEN outstanding ELSE 0 END) AS overdue_amount,
           MAX(days_past_due) AS max_days_past_due
    FROM bucketed
    GROUP BY customer_tax_code, customer_name
)
";

    private const string OnTimeBaseCte = @"
WITH invoice_alloc AS (
    SELECT ra.invoice_id, SUM(ra.amount) AS allocated
    FROM congno.receipt_allocations ra
    JOIN congno.receipts r ON r.id = ra.receipt_id
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND r.receipt_date <= @asOf
    GROUP BY ra.invoice_id
),
advance_alloc AS (
    SELECT ra.advance_id, SUM(ra.amount) AS allocated
    FROM congno.receipt_allocations ra
    JOIN congno.receipts r ON r.id = ra.receipt_id
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND r.receipt_date <= @asOf
    GROUP BY ra.advance_id
),
invoice_period AS (
    SELECT i.customer_tax_code,
           c.name AS customer_name,
           (i.total_amount - COALESCE(a.allocated, 0)) AS outstanding,
           i.total_amount AS issued_amount,
           (i.issue_date + (COALESCE(c.payment_terms_days, 0) || ' days')::interval)::date AS due_date
    FROM congno.invoices i
    JOIN congno.customers c ON c.tax_code = i.customer_tax_code
    LEFT JOIN invoice_alloc a ON a.invoice_id = i.id
    WHERE i.deleted_at IS NULL
      AND i.status <> 'VOID'
      AND i.issue_date >= @from
      AND i.issue_date <= @to
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
),
advance_period AS (
    SELECT a.customer_tax_code,
           c.name AS customer_name,
           (a.amount - COALESCE(alloc.allocated, 0)) AS outstanding,
           a.amount AS issued_amount,
           (a.advance_date + (COALESCE(c.payment_terms_days, 0) || ' days')::interval)::date AS due_date
    FROM congno.advances a
    JOIN congno.customers c ON c.tax_code = a.customer_tax_code
    LEFT JOIN advance_alloc alloc ON alloc.advance_id = a.id
    WHERE a.deleted_at IS NULL
      AND a.status IN ('APPROVED','PAID')
      AND a.advance_date >= @from
      AND a.advance_date <= @to
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
),
combined AS (
    SELECT customer_tax_code, customer_name, outstanding, issued_amount, due_date FROM invoice_period
    UNION ALL
    SELECT customer_tax_code, customer_name, outstanding, issued_amount, due_date FROM advance_period
),
due_items AS (
    SELECT *
    FROM combined
    WHERE due_date <= @asOf
),
agg AS (
    SELECT customer_tax_code,
           customer_name,
           SUM(issued_amount) AS total_due,
           SUM(outstanding) AS outstanding_due,
           MAX(GREATEST(0, (@asOf::date - due_date))::int) AS max_days_past_due
    FROM due_items
    GROUP BY customer_tax_code, customer_name
)
";

    private const string DashboardKpiSql = AgingBaseCte + @"
SELECT
    COALESCE((
        SELECT SUM(i.outstanding_amount)
        FROM congno.invoices i
        JOIN congno.customers c ON c.tax_code = i.customer_tax_code
        WHERE i.deleted_at IS NULL
          AND i.status <> 'VOID'
          AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    ), 0) AS outstandingInvoice,
    COALESCE((
        SELECT SUM(a.outstanding_amount)
        FROM congno.advances a
        JOIN congno.customers c ON c.tax_code = a.customer_tax_code
        WHERE a.deleted_at IS NULL
          AND a.status IN ('APPROVED','PAID')
          AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    ), 0) AS outstandingAdvance,
    COALESCE((
        SELECT SUM(r.amount)
        FROM congno.receipts r
        JOIN congno.customers c ON c.tax_code = r.customer_tax_code
        WHERE r.deleted_at IS NULL
          AND r.status = 'DRAFT'
          AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    ), 0) AS pendingReceiptsAmount,
    COALESCE((
        SELECT SUM(r.unallocated_amount)
        FROM congno.receipts r
        JOIN congno.customers c ON c.tax_code = r.customer_tax_code
        WHERE r.deleted_at IS NULL
          AND r.status = 'APPROVED'
          AND r.unallocated_amount > 0
          AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    ), 0) AS unallocatedReceiptsAmount,
    COALESCE((
        SELECT COUNT(*)
        FROM congno.receipts r
        JOIN congno.customers c ON c.tax_code = r.customer_tax_code
        WHERE r.deleted_at IS NULL
          AND r.status = 'DRAFT'
          AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    ), 0) AS pendingReceiptsCount,
    COALESCE((
        SELECT COUNT(*)
        FROM congno.receipts r
        JOIN congno.customers c ON c.tax_code = r.customer_tax_code
        WHERE r.deleted_at IS NULL
          AND r.status = 'APPROVED'
          AND r.unallocated_amount > 0
          AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    ), 0) AS unallocatedReceiptsCount,
    COALESCE((
        SELECT SUM(a.amount)
        FROM congno.advances a
        JOIN congno.customers c ON c.tax_code = a.customer_tax_code
        WHERE a.deleted_at IS NULL
          AND a.status = 'DRAFT'
          AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    ), 0) AS pendingAdvancesAmount,
    COALESCE((
        SELECT COUNT(*)
        FROM congno.advances a
        JOIN congno.customers c ON c.tax_code = a.customer_tax_code
        WHERE a.deleted_at IS NULL
          AND a.status = 'DRAFT'
          AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    ), 0) AS pendingAdvancesCount,
    COALESCE((
        SELECT SUM(overdue_amount) FROM agg
    ), 0) AS overdueTotal,
    COALESCE((
        SELECT COUNT(*) FROM agg WHERE overdue_amount > 0
    ), 0) AS overdueCustomers,
    COALESCE((
        SELECT COUNT(*) FROM congno.import_batches b WHERE b.status = 'STAGING'
    ), 0) AS pendingImportBatches,
    COALESCE((
        SELECT COUNT(*) FROM congno.period_locks
    ), 0) AS periodLocksCount;
";

    private const string DashboardAllocationStatusSql = @"
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
  AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
GROUP BY 1;
";

    private const string DashboardTrendSql = @"
WITH periods AS (
    SELECT generate_series(
        date_trunc(@trendGranularity, @trendSeriesFrom::timestamp),
        date_trunc(@trendGranularity, @trendSeriesTo::timestamp),
        CASE WHEN @trendGranularity = 'week' THEN interval '1 week' ELSE interval '1 month' END
    )::date AS period_start
),
invoice AS (
    SELECT date_trunc(@trendGranularity, i.issue_date)::date AS period_start,
           SUM(i.total_amount) AS invoiced_total
    FROM congno.invoices i
    JOIN congno.customers c ON c.tax_code = i.customer_tax_code
    WHERE i.deleted_at IS NULL
      AND i.status <> 'VOID'
      AND i.issue_date >= @trendFilterFrom
      AND i.issue_date <= @trendFilterTo
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    GROUP BY date_trunc(@trendGranularity, i.issue_date)
),
advance AS (
    SELECT date_trunc(@trendGranularity, a.advance_date)::date AS period_start,
           SUM(a.amount) AS advanced_total
    FROM congno.advances a
    JOIN congno.customers c ON c.tax_code = a.customer_tax_code
    WHERE a.deleted_at IS NULL
      AND a.status IN ('APPROVED','PAID')
      AND a.advance_date >= @trendFilterFrom
      AND a.advance_date <= @trendFilterTo
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    GROUP BY date_trunc(@trendGranularity, a.advance_date)
),
receipt AS (
    SELECT date_trunc(@trendGranularity, COALESCE(r.approved_at, r.receipt_date::timestamp))::date AS period_start,
           SUM(r.amount) AS receipted_total
    FROM congno.receipts r
    JOIN congno.customers c ON c.tax_code = r.customer_tax_code
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND COALESCE(r.approved_at, r.receipt_date::timestamp)::date >= @trendFilterFrom
      AND COALESCE(r.approved_at, r.receipt_date::timestamp)::date <= @trendFilterTo
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    GROUP BY date_trunc(@trendGranularity, COALESCE(r.approved_at, r.receipt_date::timestamp))
)
SELECT
    CASE
        WHEN @trendGranularity = 'week' THEN to_char(p.period_start, 'IYYY-""W""IW')
        ELSE to_char(p.period_start, 'YYYY-MM')
    END AS period,
    COALESCE(i.invoiced_total, 0) AS invoicedTotal,
    COALESCE(a.advanced_total, 0) AS advancedTotal,
    COALESCE(r.receipted_total, 0) AS receiptedTotal
FROM periods p
LEFT JOIN invoice i ON i.period_start = p.period_start
LEFT JOIN advance a ON a.period_start = p.period_start
LEFT JOIN receipt r ON r.period_start = p.period_start
ORDER BY p.period_start;
";

    private const string DashboardTopOutstandingSql = AgingBaseCte + @"
SELECT customer_tax_code AS customerTaxCode,
       customer_name AS customerName,
       total_outstanding AS amount,
       max_days_past_due AS daysPastDue
FROM agg
ORDER BY total_outstanding DESC
LIMIT @top;
";

    private const string DashboardTopOnTimeSql = OnTimeBaseCte + @"
SELECT customer_tax_code AS customerTaxCode,
       customer_name AS customerName,
       total_due AS amount,
       max_days_past_due AS daysPastDue
FROM agg
WHERE total_due > 0
ORDER BY COALESCE((total_due - outstanding_due) / NULLIF(total_due, 0), 0) DESC,
         total_due DESC,
         max_days_past_due ASC
LIMIT @top;
";

    private const string DashboardOnTimeCountSql = OnTimeBaseCte + @"
SELECT COUNT(*) AS onTimeCustomers
FROM agg
WHERE total_due > 0
  AND COALESCE((total_due - outstanding_due) / NULLIF(total_due, 0), 0) >= @onTimeThreshold;
";

    private const string DashboardTopOverdueDaysSql = AgingBaseCte + @"
SELECT customer_tax_code AS customerTaxCode,
       customer_name AS customerName,
       overdue_amount AS amount,
       max_days_past_due AS daysPastDue
FROM agg
WHERE max_days_past_due > 0
ORDER BY max_days_past_due DESC, overdue_amount DESC
LIMIT @top;
";

    private const string DashboardOverdueByCustomerSql = AgingBaseCte + @"
SELECT customer_tax_code AS groupKey,
       customer_name AS groupName,
       SUM(outstanding) AS totalOutstanding,
       SUM(CASE WHEN days_past_due > 0 THEN outstanding ELSE 0 END) AS overdueAmount,
       COALESCE(SUM(CASE WHEN days_past_due > 0 THEN outstanding ELSE 0 END) / NULLIF(SUM(outstanding), 0), 0) AS overdueRatio,
       COUNT(DISTINCT CASE WHEN days_past_due > 0 THEN customer_tax_code END) AS overdueCustomers
FROM bucketed
GROUP BY customer_tax_code, customer_name
ORDER BY overdueAmount DESC, totalOutstanding DESC
LIMIT @top;
";


    private const string DashboardOverdueByOwnerSql = AgingBaseCte + @"
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

    private const string DashboardAgingBucketsSql = AgingBaseCte + @"
SELECT bucket, amount FROM (
    SELECT '0-30' AS bucket, SUM(CASE WHEN days_past_due <= 30 THEN outstanding ELSE 0 END) AS amount, 1 AS sort
    FROM bucketed
    UNION ALL
    SELECT '31-60' AS bucket, SUM(CASE WHEN days_past_due BETWEEN 31 AND 60 THEN outstanding ELSE 0 END), 2
    FROM bucketed
    UNION ALL
    SELECT '61-90' AS bucket, SUM(CASE WHEN days_past_due BETWEEN 61 AND 90 THEN outstanding ELSE 0 END), 3
    FROM bucketed
    UNION ALL
    SELECT '91-180' AS bucket, SUM(CASE WHEN days_past_due BETWEEN 91 AND 180 THEN outstanding ELSE 0 END), 4
    FROM bucketed
    UNION ALL
    SELECT '>180' AS bucket, SUM(CASE WHEN days_past_due > 180 THEN outstanding ELSE 0 END), 5
    FROM bucketed
) buckets
ORDER BY sort;
";

    private const string DashboardLastUpdatedSql = @"
SELECT GREATEST(
    COALESCE((SELECT MAX(updated_at) FROM congno.invoices WHERE deleted_at IS NULL), to_timestamp(0)),
    COALESCE((SELECT MAX(updated_at) FROM congno.advances WHERE deleted_at IS NULL), to_timestamp(0)),
    COALESCE((SELECT MAX(updated_at) FROM congno.receipts WHERE deleted_at IS NULL), to_timestamp(0)),
    COALESCE((SELECT MAX(updated_at) FROM congno.customers), to_timestamp(0)),
    COALESCE((SELECT MAX(created_at) FROM congno.import_batches), to_timestamp(0)),
    COALESCE((SELECT MAX(locked_at) FROM congno.period_locks), to_timestamp(0))
) AS lastUpdatedAt;
";
}
