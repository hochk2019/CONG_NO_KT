namespace CongNoGolden.Infrastructure.Services;

public sealed partial class RiskService
{
    private const string RiskBaseCte = @"
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
    SELECT i.id AS doc_id,
           'INVOICE' AS doc_type,
           i.customer_tax_code,
           c.name AS customer_name,
           c.accountant_owner_id AS owner_id,
           (i.total_amount - COALESCE(a.allocated, 0)) AS outstanding,
           (i.issue_date + (COALESCE(c.payment_terms_days, 0) || ' days')::interval)::date AS due_date
    FROM congno.invoices i
    JOIN congno.customers c ON c.tax_code = i.customer_tax_code
    LEFT JOIN invoice_alloc a ON a.invoice_id = i.id
    WHERE i.deleted_at IS NULL
      AND i.status <> 'VOID'
),
advance_out AS (
    SELECT a.id AS doc_id,
           'ADVANCE' AS doc_type,
           a.customer_tax_code,
           c.name AS customer_name,
           c.accountant_owner_id AS owner_id,
           (a.amount - COALESCE(alloc.allocated, 0)) AS outstanding,
           (a.advance_date + (COALESCE(c.payment_terms_days, 0) || ' days')::interval)::date AS due_date
    FROM congno.advances a
    JOIN congno.customers c ON c.tax_code = a.customer_tax_code
    LEFT JOIN advance_alloc alloc ON alloc.advance_id = a.id
    WHERE a.deleted_at IS NULL
      AND a.status IN ('APPROVED','PAID')
),
combined AS (
    SELECT * FROM invoice_out
    UNION ALL
    SELECT * FROM advance_out
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
           owner_id,
           SUM(outstanding) AS total_outstanding,
           SUM(CASE WHEN days_past_due > 0 THEN outstanding ELSE 0 END) AS overdue_amount,
           MAX(days_past_due) AS max_days_past_due,
           COUNT(*) FILTER (WHERE days_past_due > 0) AS late_count
    FROM bucketed
    GROUP BY customer_tax_code, customer_name, owner_id
),
agg_ratio AS (
    SELECT *,
           COALESCE(overdue_amount / NULLIF(total_outstanding, 0), 0) AS overdue_ratio
    FROM agg
),
rules AS (
    SELECT
        COUNT(*) FILTER (WHERE is_active = true) AS active_count,
        COALESCE(MAX(CASE WHEN level = 'VERY_HIGH' THEN match_mode END), 'ANY') AS vh_mode,
        COALESCE(MAX(CASE WHEN level = 'VERY_HIGH' THEN min_overdue_days END), 0) AS vh_days,
        COALESCE(MAX(CASE WHEN level = 'VERY_HIGH' THEN min_overdue_ratio END), 0) AS vh_ratio,
        COALESCE(MAX(CASE WHEN level = 'VERY_HIGH' THEN min_late_count END), 0) AS vh_late,
        COALESCE(MAX(CASE WHEN level = 'HIGH' THEN match_mode END), 'ANY') AS h_mode,
        COALESCE(MAX(CASE WHEN level = 'HIGH' THEN min_overdue_days END), 0) AS h_days,
        COALESCE(MAX(CASE WHEN level = 'HIGH' THEN min_overdue_ratio END), 0) AS h_ratio,
        COALESCE(MAX(CASE WHEN level = 'HIGH' THEN min_late_count END), 0) AS h_late,
        COALESCE(MAX(CASE WHEN level = 'MEDIUM' THEN match_mode END), 'ANY') AS m_mode,
        COALESCE(MAX(CASE WHEN level = 'MEDIUM' THEN min_overdue_days END), 0) AS m_days,
        COALESCE(MAX(CASE WHEN level = 'MEDIUM' THEN min_overdue_ratio END), 0) AS m_ratio,
        COALESCE(MAX(CASE WHEN level = 'MEDIUM' THEN min_late_count END), 0) AS m_late
    FROM congno.risk_rules
    WHERE is_active = true
),
classified AS (
    SELECT a.customer_tax_code,
           a.customer_name,
           a.owner_id,
           COALESCE(u.full_name, u.username) AS owner_name,
           a.total_outstanding,
           a.overdue_amount,
           a.overdue_ratio,
           a.max_days_past_due,
           a.late_count,
           CASE
               WHEN r.active_count = 0 THEN 'LOW'
               WHEN (
                   (r.vh_mode = 'ALL' AND a.max_days_past_due >= r.vh_days AND a.overdue_ratio >= r.vh_ratio AND a.late_count >= r.vh_late)
                   OR
                   (r.vh_mode <> 'ALL' AND (a.max_days_past_due >= r.vh_days OR a.overdue_ratio >= r.vh_ratio OR a.late_count >= r.vh_late))
               ) THEN 'VERY_HIGH'
               WHEN (
                   (r.h_mode = 'ALL' AND a.max_days_past_due >= r.h_days AND a.overdue_ratio >= r.h_ratio AND a.late_count >= r.h_late)
                   OR
                   (r.h_mode <> 'ALL' AND (a.max_days_past_due >= r.h_days OR a.overdue_ratio >= r.h_ratio OR a.late_count >= r.h_late))
               ) THEN 'HIGH'
               WHEN (
                   (r.m_mode = 'ALL' AND a.max_days_past_due >= r.m_days AND a.overdue_ratio >= r.m_ratio AND a.late_count >= r.m_late)
                   OR
                   (r.m_mode <> 'ALL' AND (a.max_days_past_due >= r.m_days OR a.overdue_ratio >= r.m_ratio OR a.late_count >= r.m_late))
               ) THEN 'MEDIUM'
               ELSE 'LOW'
           END AS risk_level,
           CASE
               WHEN r.active_count = 0 THEN 1
               WHEN (
                   (r.vh_mode = 'ALL' AND a.max_days_past_due >= r.vh_days AND a.overdue_ratio >= r.vh_ratio AND a.late_count >= r.vh_late)
                   OR
                   (r.vh_mode <> 'ALL' AND (a.max_days_past_due >= r.vh_days OR a.overdue_ratio >= r.vh_ratio OR a.late_count >= r.vh_late))
               ) THEN 4
               WHEN (
                   (r.h_mode = 'ALL' AND a.max_days_past_due >= r.h_days AND a.overdue_ratio >= r.h_ratio AND a.late_count >= r.h_late)
                   OR
                   (r.h_mode <> 'ALL' AND (a.max_days_past_due >= r.h_days OR a.overdue_ratio >= r.h_ratio OR a.late_count >= r.h_late))
               ) THEN 3
               WHEN (
                   (r.m_mode = 'ALL' AND a.max_days_past_due >= r.m_days AND a.overdue_ratio >= r.m_ratio AND a.late_count >= r.m_late)
                   OR
                   (r.m_mode <> 'ALL' AND (a.max_days_past_due >= r.m_days OR a.overdue_ratio >= r.m_ratio OR a.late_count >= r.m_late))
               ) THEN 2
               ELSE 1
           END AS risk_rank
    FROM agg_ratio a
    CROSS JOIN rules r
    LEFT JOIN congno.users u ON u.id = a.owner_id
    WHERE a.total_outstanding > 0
)
";

    private const string RiskOverviewSql = RiskBaseCte + @"
SELECT risk_level AS level,
       COUNT(*) AS customers,
       SUM(total_outstanding) AS totalOutstanding,
       SUM(overdue_amount) AS overdueAmount,
       MAX(risk_rank) AS riskRank
FROM classified
WHERE (@ownerId IS NULL OR owner_id = @ownerId)
GROUP BY risk_level
ORDER BY riskRank DESC;
";

    private const string RiskListSqlTemplate = @"
{0}
SELECT COUNT(*) FROM classified
WHERE (@ownerId IS NULL OR owner_id = @ownerId)
  AND (@level IS NULL OR risk_level = @level)
  AND (@search IS NULL OR customer_name ILIKE @search OR customer_tax_code ILIKE @search);

{0}
SELECT customer_tax_code AS customerTaxCode,
       customer_name AS customerName,
       owner_id AS ownerId,
       owner_name AS ownerName,
       total_outstanding AS totalOutstanding,
       overdue_amount AS overdueAmount,
       overdue_ratio AS overdueRatio,
       max_days_past_due AS maxDaysPastDue,
       late_count AS lateCount,
       risk_level AS riskLevel
FROM classified
WHERE (@ownerId IS NULL OR owner_id = @ownerId)
  AND (@level IS NULL OR risk_level = @level)
  AND (@search IS NULL OR customer_name ILIKE @search OR customer_tax_code ILIKE @search)
ORDER BY {1}
LIMIT @limit OFFSET @offset;
";
}
