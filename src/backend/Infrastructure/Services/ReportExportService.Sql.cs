namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReportExportService
{
    private const string SummarySql = @"
WITH invoice_before AS (
    SELECT customer_tax_code, SUM(total_amount) AS total
    FROM congno.invoices
    WHERE deleted_at IS NULL
      AND issue_date < @from
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR customer_tax_code = @customerTaxCode)
    GROUP BY customer_tax_code
),
advance_before AS (
    SELECT customer_tax_code, SUM(amount) AS total
    FROM congno.advances
    WHERE deleted_at IS NULL
      AND status IN ('APPROVED','PAID')
      AND advance_date < @from
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR customer_tax_code = @customerTaxCode)
    GROUP BY customer_tax_code
),
receipt_before AS (
    SELECT r.customer_tax_code, SUM(ra.amount) AS total
    FROM congno.receipts r
    JOIN congno.receipt_allocations ra ON ra.receipt_id = r.id
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND COALESCE(r.applied_period_start, r.receipt_date) < @from
      AND (@sellerTaxCode IS NULL OR r.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR r.customer_tax_code = @customerTaxCode)
    GROUP BY r.customer_tax_code
),
invoice_period AS (
    SELECT customer_tax_code, SUM(total_amount) AS total
    FROM congno.invoices
    WHERE deleted_at IS NULL
      AND issue_date >= @from AND issue_date <= @to
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR customer_tax_code = @customerTaxCode)
    GROUP BY customer_tax_code
),
advance_period AS (
    SELECT customer_tax_code, SUM(amount) AS total
    FROM congno.advances
    WHERE deleted_at IS NULL
      AND status IN ('APPROVED','PAID')
      AND advance_date >= @from AND advance_date <= @to
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR customer_tax_code = @customerTaxCode)
    GROUP BY customer_tax_code
),
receipt_period AS (
    SELECT r.customer_tax_code, SUM(ra.amount) AS total
    FROM congno.receipts r
    JOIN congno.receipt_allocations ra ON ra.receipt_id = r.id
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND COALESCE(r.applied_period_start, r.receipt_date) >= @from
      AND COALESCE(r.applied_period_start, r.receipt_date) <= @to
      AND (@sellerTaxCode IS NULL OR r.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR r.customer_tax_code = @customerTaxCode)
    GROUP BY r.customer_tax_code
),
invoice_alloc AS (
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
           (i.total_amount - COALESCE(a.allocated, 0)) AS outstanding,
           (i.issue_date + (COALESCE(c.payment_terms_days, 0) || ' days')::interval)::date AS due_date
    FROM congno.invoices i
    JOIN congno.customers c ON c.tax_code = i.customer_tax_code
    LEFT JOIN invoice_alloc a ON a.invoice_id = i.id
    WHERE i.deleted_at IS NULL
      AND (@sellerTaxCode IS NULL OR i.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR i.customer_tax_code = @customerTaxCode)
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
),
advance_out AS (
    SELECT a.customer_tax_code,
           (a.amount - COALESCE(alloc.allocated, 0)) AS outstanding,
           (a.advance_date + (COALESCE(c.payment_terms_days, 0) || ' days')::interval)::date AS due_date
    FROM congno.advances a
    JOIN congno.customers c ON c.tax_code = a.customer_tax_code
    LEFT JOIN advance_alloc alloc ON alloc.advance_id = a.id
    WHERE a.deleted_at IS NULL
      AND a.status IN ('APPROVED','PAID')
      AND (@sellerTaxCode IS NULL OR a.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR a.customer_tax_code = @customerTaxCode)
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
),
combined AS (
    SELECT * FROM invoice_out
    UNION ALL
    SELECT * FROM advance_out
),
aging_summary AS (
    SELECT customer_tax_code,
           SUM(CASE WHEN days_past_due > 0 THEN outstanding ELSE 0 END) AS overdue,
           MAX(CASE WHEN days_past_due > 0 THEN days_past_due ELSE 0 END) AS max_days
    FROM (
        SELECT customer_tax_code,
               outstanding,
               (@asOf::date - due_date)::int AS days_past_due
        FROM combined
        WHERE outstanding > 0
    ) a
    GROUP BY customer_tax_code
)
SELECT c.tax_code AS customer_tax_code,
       c.name AS customer_name,
       COALESCE(u.full_name, u.username) AS owner_name,
       COALESCE(ib.total, 0) + COALESCE(ab.total, 0) - COALESCE(rb.total, 0) AS opening_balance,
       COALESCE(ip.total, 0) AS invoiced_total,
       COALESCE(ap.total, 0) AS advanced_total,
       COALESCE(rp.total, 0) AS receipted_total,
       0::numeric AS adjustments,
       (COALESCE(ib.total, 0) + COALESCE(ab.total, 0) - COALESCE(rb.total, 0)
        + COALESCE(ip.total, 0) + COALESCE(ap.total, 0) - COALESCE(rp.total, 0)) AS closing_balance,
       COALESCE(ag.overdue, 0) AS overdue_amount,
       COALESCE(ag.max_days, 0) AS max_age_days
FROM congno.customers c
LEFT JOIN invoice_before ib ON ib.customer_tax_code = c.tax_code
LEFT JOIN advance_before ab ON ab.customer_tax_code = c.tax_code
LEFT JOIN receipt_before rb ON rb.customer_tax_code = c.tax_code
LEFT JOIN invoice_period ip ON ip.customer_tax_code = c.tax_code
LEFT JOIN advance_period ap ON ap.customer_tax_code = c.tax_code
LEFT JOIN receipt_period rp ON rp.customer_tax_code = c.tax_code
LEFT JOIN aging_summary ag ON ag.customer_tax_code = c.tax_code
LEFT JOIN congno.users u ON u.id = c.accountant_owner_id
WHERE (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
  AND (@customerTaxCode IS NULL OR c.tax_code = @customerTaxCode)
ORDER BY c.name;
";

    private const string DetailSql = @"
WITH receipt_alloc AS (
    SELECT ra.receipt_id, SUM(ra.amount) AS allocated
    FROM congno.receipt_allocations ra
    JOIN congno.receipts r ON r.id = ra.receipt_id
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
    GROUP BY ra.receipt_id
)
SELECT i.issue_date AS document_date,
       NULL::date AS applied_period_start,
       'INVOICE' AS type,
       i.seller_tax_code,
       i.customer_tax_code,
       c.name AS customer_name,
       i.invoice_no AS document_no,
       i.note AS description,
       i.revenue_excl_vat AS revenue,
       i.vat_amount AS vat,
       i.total_amount AS increase,
       0::numeric AS decrease,
       0::numeric AS running_balance,
       NULL::text AS created_by,
       NULL::text AS approved_by,
       i.source_batch_id::text AS batch
FROM congno.invoices i
JOIN congno.customers c ON c.tax_code = i.customer_tax_code
WHERE i.deleted_at IS NULL
  AND i.issue_date >= @from AND i.issue_date <= @to
  AND (@sellerTaxCode IS NULL OR i.seller_tax_code = @sellerTaxCode)
  AND (@customerTaxCode IS NULL OR i.customer_tax_code = @customerTaxCode)
  AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)

UNION ALL

SELECT a.advance_date AS document_date,
       NULL::date AS applied_period_start,
       'ADVANCE' AS type,
       a.seller_tax_code,
       a.customer_tax_code,
       c.name AS customer_name,
       a.id::text AS document_no,
       a.description AS description,
       0::numeric AS revenue,
       0::numeric AS vat,
       a.amount AS increase,
       0::numeric AS decrease,
       0::numeric AS running_balance,
       COALESCE(u_created.full_name, u_created.username) AS created_by,
       COALESCE(u_approved.full_name, u_approved.username) AS approved_by,
       a.source_batch_id::text AS batch
FROM congno.advances a
JOIN congno.customers c ON c.tax_code = a.customer_tax_code
LEFT JOIN congno.users u_created ON u_created.id = a.created_by
LEFT JOIN congno.users u_approved ON u_approved.id = a.approved_by
WHERE a.deleted_at IS NULL
  AND a.status IN ('APPROVED','PAID')
  AND a.advance_date >= @from AND a.advance_date <= @to
  AND (@sellerTaxCode IS NULL OR a.seller_tax_code = @sellerTaxCode)
  AND (@customerTaxCode IS NULL OR a.customer_tax_code = @customerTaxCode)
  AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)

UNION ALL

SELECT r.receipt_date AS document_date,
       r.applied_period_start AS applied_period_start,
       'RECEIPT' AS type,
       r.seller_tax_code,
       r.customer_tax_code,
       c.name AS customer_name,
       r.id::text AS document_no,
       r.description AS description,
       0::numeric AS revenue,
       0::numeric AS vat,
       0::numeric AS increase,
       COALESCE(ra.allocated, 0) AS decrease,
       0::numeric AS running_balance,
       COALESCE(u_created.full_name, u_created.username) AS created_by,
       COALESCE(u_approved.full_name, u_approved.username) AS approved_by,
       r.source_batch_id::text AS batch
FROM congno.receipts r
JOIN congno.customers c ON c.tax_code = r.customer_tax_code
LEFT JOIN receipt_alloc ra ON ra.receipt_id = r.id
LEFT JOIN congno.users u_created ON u_created.id = r.created_by
LEFT JOIN congno.users u_approved ON u_approved.id = r.approved_by
WHERE r.deleted_at IS NULL
  AND r.status = 'APPROVED'
  AND r.receipt_date >= @from AND r.receipt_date <= @to
  AND (@sellerTaxCode IS NULL OR r.seller_tax_code = @sellerTaxCode)
  AND (@customerTaxCode IS NULL OR r.customer_tax_code = @customerTaxCode)
  AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
  AND COALESCE(ra.allocated, 0) > 0;
";

    private const string SellerNameSql = "SELECT name FROM congno.sellers WHERE seller_tax_code = @sellerTaxCode";
}
