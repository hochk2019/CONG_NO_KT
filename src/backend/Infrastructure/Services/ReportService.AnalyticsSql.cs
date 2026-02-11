namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReportService
{
    private const string ReportAgingBaseCte = @"
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
      AND i.issue_date <= @asOf
      AND (@sellerTaxCode IS NULL OR i.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR i.customer_tax_code = @customerTaxCode)
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
      AND a.advance_date <= @asOf
      AND (@sellerTaxCode IS NULL OR a.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR a.customer_tax_code = @customerTaxCode)
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

    private const string ReportOnTimeBaseCte = @"
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
      AND (@sellerTaxCode IS NULL OR i.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR i.customer_tax_code = @customerTaxCode)
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
      AND (@sellerTaxCode IS NULL OR a.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR a.customer_tax_code = @customerTaxCode)
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
),
combined AS (
    SELECT customer_tax_code, customer_name, outstanding, issued_amount, due_date FROM invoice_period
    UNION ALL
    SELECT customer_tax_code, customer_name, outstanding, issued_amount, due_date FROM advance_period
),
agg AS (
    SELECT customer_tax_code,
           customer_name,
           SUM(issued_amount) AS total_due,
           SUM(outstanding) AS outstanding_due,
           MAX(GREATEST(0, (@asOf::date - due_date))::int) AS max_days_past_due
    FROM combined
    GROUP BY customer_tax_code, customer_name
)
";
}
