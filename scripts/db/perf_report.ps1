param(
  [string]$ConnectionString = $env:CONGNO_PERF_CONN,
  [string]$OutputPath = "tmp/perf/perf_report.txt"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
  $ConnectionString = "Host=localhost;Port=5432;Username=postgres;Password=Postgres@123;Database=congno_golden"
}

$outputDir = Split-Path -Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
  New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}

$shared = "C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App\8.0.22"
$deps = @(
  "Microsoft.Extensions.Logging.Abstractions.dll",
  "Microsoft.Extensions.DependencyInjection.Abstractions.dll"
)
foreach ($dep in $deps) {
  $path = Join-Path $shared $dep
  if (Test-Path $path) {
    [System.Reflection.Assembly]::LoadFrom($path) | Out-Null
  }
}
Add-Type -Path "src\backend\Api\bin\Debug\net8.0\Npgsql.dll"

$conn = New-Object Npgsql.NpgsqlConnection($ConnectionString)
$conn.Open()

function Get-Scalar {
  param([string]$Sql)
  $cmd = $conn.CreateCommand()
  $cmd.CommandText = $Sql
  return $cmd.ExecuteScalar()
}

$fromDate = (Get-Date).AddDays(-365).ToString("yyyy-MM-dd")
$toDate = (Get-Date).ToString("yyyy-MM-dd")
$asOfDate = $toDate

$customerTaxCode = Get-Scalar "SELECT tax_code FROM congno.customers ORDER BY created_at NULLS LAST LIMIT 1"
$sellerTaxCode = Get-Scalar "SELECT seller_tax_code FROM congno.sellers ORDER BY created_at NULLS LAST LIMIT 1"

function SqlTextOrNull {
  param([string]$Value)
  if ([string]::IsNullOrWhiteSpace($Value)) {
    return "NULL"
  }
  return "'" + $Value.Replace("'", "''") + "'"
}

$fromLiteral = "'" + $fromDate + "'::date"
$toLiteral = "'" + $toDate + "'::date"
$asOfLiteral = "'" + $asOfDate + "'::date"
$customerLiteral = SqlTextOrNull $customerTaxCode
$sellerLiteral = SqlTextOrNull $sellerTaxCode
$customerFilter = "NULL"
$sellerFilter = "NULL"
$ownerLiteral = "NULL::uuid"

$queries = @()

$queries += [pscustomobject]@{
  Name = "summary_customer"
  Sql = @"
WITH invoice AS (
    SELECT customer_tax_code AS key,
           SUM(total_amount) AS invoiced_total,
           SUM(outstanding_amount) AS outstanding_invoice
    FROM congno.invoices
    WHERE deleted_at IS NULL
      AND ($fromLiteral IS NULL OR issue_date >= $fromLiteral)
      AND ($toLiteral IS NULL OR issue_date <= $toLiteral)
      AND ($sellerFilter IS NULL OR seller_tax_code = $sellerFilter)
      AND ($customerFilter IS NULL OR customer_tax_code = $customerFilter)
    GROUP BY customer_tax_code
),
advance AS (
    SELECT customer_tax_code AS key,
           SUM(amount) AS advanced_total,
           SUM(outstanding_amount) AS outstanding_advance
    FROM congno.advances
    WHERE deleted_at IS NULL
      AND ($fromLiteral IS NULL OR advance_date >= $fromLiteral)
      AND ($toLiteral IS NULL OR advance_date <= $toLiteral)
      AND ($sellerFilter IS NULL OR seller_tax_code = $sellerFilter)
      AND ($customerFilter IS NULL OR customer_tax_code = $customerFilter)
    GROUP BY customer_tax_code
),
receipt AS (
    SELECT customer_tax_code AS key,
           SUM(amount) AS receipted_total
    FROM congno.receipts
    WHERE deleted_at IS NULL
      AND status = 'APPROVED'
      AND ($fromLiteral IS NULL OR receipt_date >= $fromLiteral)
      AND ($toLiteral IS NULL OR receipt_date <= $toLiteral)
      AND ($sellerFilter IS NULL OR seller_tax_code = $sellerFilter)
      AND ($customerFilter IS NULL OR customer_tax_code = $customerFilter)
    GROUP BY customer_tax_code
)
SELECT c.tax_code AS group_key,
       c.name AS group_name,
       COALESCE(i.invoiced_total, 0) AS invoiced_total,
       COALESCE(a.advanced_total, 0) AS advanced_total,
       COALESCE(r.receipted_total, 0) AS receipted_total,
       COALESCE(i.outstanding_invoice, 0) AS outstanding_invoice,
       COALESCE(a.outstanding_advance, 0) AS outstanding_advance,
       COALESCE(c.current_balance, 0) AS current_balance
FROM congno.customers c
LEFT JOIN invoice i ON i.key = c.tax_code
LEFT JOIN advance a ON a.key = c.tax_code
LEFT JOIN receipt r ON r.key = c.tax_code
WHERE ($ownerLiteral IS NULL OR c.accountant_owner_id = $ownerLiteral)
  AND ($customerFilter IS NULL OR c.tax_code = $customerFilter)
ORDER BY c.name;
"@
}

$queries += [pscustomobject]@{
  Name = "summary_seller"
  Sql = @"
WITH invoice AS (
    SELECT seller_tax_code AS key,
           SUM(total_amount) AS invoiced_total,
           SUM(outstanding_amount) AS outstanding_invoice
    FROM congno.invoices
    WHERE deleted_at IS NULL
      AND ($fromLiteral IS NULL OR issue_date >= $fromLiteral)
      AND ($toLiteral IS NULL OR issue_date <= $toLiteral)
      AND ($sellerLiteral IS NULL OR seller_tax_code = $sellerLiteral)
      AND ($customerFilter IS NULL OR customer_tax_code = $customerFilter)
    GROUP BY seller_tax_code
),
advance AS (
    SELECT seller_tax_code AS key,
           SUM(amount) AS advanced_total,
           SUM(outstanding_amount) AS outstanding_advance
    FROM congno.advances
    WHERE deleted_at IS NULL
      AND ($fromLiteral IS NULL OR advance_date >= $fromLiteral)
      AND ($toLiteral IS NULL OR advance_date <= $toLiteral)
      AND ($sellerLiteral IS NULL OR seller_tax_code = $sellerLiteral)
      AND ($customerFilter IS NULL OR customer_tax_code = $customerFilter)
    GROUP BY seller_tax_code
),
receipt AS (
    SELECT seller_tax_code AS key,
           SUM(amount) AS receipted_total
    FROM congno.receipts
    WHERE deleted_at IS NULL
      AND status = 'APPROVED'
      AND ($fromLiteral IS NULL OR receipt_date >= $fromLiteral)
      AND ($toLiteral IS NULL OR receipt_date <= $toLiteral)
      AND ($sellerLiteral IS NULL OR seller_tax_code = $sellerLiteral)
      AND ($customerFilter IS NULL OR customer_tax_code = $customerFilter)
    GROUP BY seller_tax_code
)
SELECT s.seller_tax_code AS group_key,
       s.name AS group_name,
       COALESCE(i.invoiced_total, 0) AS invoiced_total,
       COALESCE(a.advanced_total, 0) AS advanced_total,
       COALESCE(r.receipted_total, 0) AS receipted_total,
       COALESCE(i.outstanding_invoice, 0) AS outstanding_invoice,
       COALESCE(a.outstanding_advance, 0) AS outstanding_advance,
       0 AS current_balance
FROM congno.sellers s
LEFT JOIN invoice i ON i.key = s.seller_tax_code
LEFT JOIN advance a ON a.key = s.seller_tax_code
LEFT JOIN receipt r ON r.key = s.seller_tax_code
WHERE ($sellerLiteral IS NULL OR s.seller_tax_code = $sellerLiteral)
ORDER BY s.name;
"@
}

$queries += [pscustomobject]@{
  Name = "summary_period"
  Sql = @"
SELECT period_key AS group_key,
       period_key AS group_name,
       SUM(invoiced_total) AS invoiced_total,
       SUM(advanced_total) AS advanced_total,
       SUM(receipted_total) AS receipted_total,
       SUM(outstanding_invoice) AS outstanding_invoice,
       SUM(outstanding_advance) AS outstanding_advance,
       0 AS current_balance
FROM (
    SELECT to_char(i.issue_date, 'YYYY-MM') AS period_key,
           SUM(i.total_amount) AS invoiced_total,
           SUM(i.outstanding_amount) AS outstanding_invoice,
           0::numeric AS advanced_total,
           0::numeric AS outstanding_advance,
           0::numeric AS receipted_total
    FROM congno.invoices i
    WHERE i.deleted_at IS NULL
      AND ($fromLiteral IS NULL OR i.issue_date >= $fromLiteral)
      AND ($toLiteral IS NULL OR i.issue_date <= $toLiteral)
      AND ($sellerFilter IS NULL OR i.seller_tax_code = $sellerFilter)
      AND ($customerFilter IS NULL OR i.customer_tax_code = $customerFilter)
      AND ($ownerLiteral IS NULL OR EXISTS (
          SELECT 1 FROM congno.customers c
          WHERE c.tax_code = i.customer_tax_code AND c.accountant_owner_id = $ownerLiteral
      ))
    GROUP BY to_char(i.issue_date, 'YYYY-MM')

    UNION ALL

    SELECT to_char(a.advance_date, 'YYYY-MM') AS period_key,
           0::numeric AS invoiced_total,
           0::numeric AS outstanding_invoice,
           SUM(a.amount) AS advanced_total,
           SUM(a.outstanding_amount) AS outstanding_advance,
           0::numeric AS receipted_total
    FROM congno.advances a
    WHERE a.deleted_at IS NULL
      AND ($fromLiteral IS NULL OR a.advance_date >= $fromLiteral)
      AND ($toLiteral IS NULL OR a.advance_date <= $toLiteral)
      AND ($sellerFilter IS NULL OR a.seller_tax_code = $sellerFilter)
      AND ($customerFilter IS NULL OR a.customer_tax_code = $customerFilter)
      AND ($ownerLiteral IS NULL OR EXISTS (
          SELECT 1 FROM congno.customers c
          WHERE c.tax_code = a.customer_tax_code AND c.accountant_owner_id = $ownerLiteral
      ))
    GROUP BY to_char(a.advance_date, 'YYYY-MM')

    UNION ALL

    SELECT to_char(r.receipt_date, 'YYYY-MM') AS period_key,
           0::numeric AS invoiced_total,
           0::numeric AS outstanding_invoice,
           0::numeric AS advanced_total,
           0::numeric AS outstanding_advance,
           SUM(r.amount) AS receipted_total
    FROM congno.receipts r
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND ($fromLiteral IS NULL OR r.receipt_date >= $fromLiteral)
      AND ($toLiteral IS NULL OR r.receipt_date <= $toLiteral)
      AND ($sellerFilter IS NULL OR r.seller_tax_code = $sellerFilter)
      AND ($customerFilter IS NULL OR r.customer_tax_code = $customerFilter)
      AND ($ownerLiteral IS NULL OR EXISTS (
          SELECT 1 FROM congno.customers c
          WHERE c.tax_code = r.customer_tax_code AND c.accountant_owner_id = $ownerLiteral
      ))
    GROUP BY to_char(r.receipt_date, 'YYYY-MM')
) s
GROUP BY period_key
ORDER BY period_key;
"@
}

if (-not [string]::IsNullOrWhiteSpace($customerTaxCode)) {
  $queries += [pscustomobject]@{
    Name = "statement_opening"
    Sql = @"
WITH invoices AS (
    SELECT SUM(total_amount) AS total
    FROM congno.invoices
    WHERE deleted_at IS NULL
      AND customer_tax_code = $customerLiteral
      AND ($sellerFilter IS NULL OR seller_tax_code = $sellerFilter)
      AND issue_date < $fromLiteral
),
advances AS (
    SELECT SUM(amount) AS total
    FROM congno.advances
    WHERE deleted_at IS NULL
      AND status IN ('APPROVED','PAID')
      AND customer_tax_code = $customerLiteral
      AND ($sellerFilter IS NULL OR seller_tax_code = $sellerFilter)
      AND advance_date < $fromLiteral
),
receipts AS (
    SELECT SUM(ra.amount) AS total
    FROM congno.receipt_allocations ra
    JOIN congno.receipts r ON r.id = ra.receipt_id
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND r.customer_tax_code = $customerLiteral
      AND ($sellerFilter IS NULL OR r.seller_tax_code = $sellerFilter)
      AND r.receipt_date < $fromLiteral
)
SELECT COALESCE(i.total, 0) + COALESCE(a.total, 0) - COALESCE(r.total, 0) AS opening_balance
FROM invoices i, advances a, receipts r;
"@
  }

  $queries += [pscustomobject]@{
    Name = "statement_lines"
    Sql = @"
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
  AND i.customer_tax_code = $customerLiteral
  AND ($sellerFilter IS NULL OR i.seller_tax_code = $sellerFilter)
  AND i.issue_date >= $fromLiteral AND i.issue_date <= $toLiteral

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
  AND a.customer_tax_code = $customerLiteral
  AND ($sellerFilter IS NULL OR a.seller_tax_code = $sellerFilter)
  AND a.advance_date >= $fromLiteral AND a.advance_date <= $toLiteral

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
  AND r.customer_tax_code = $customerLiteral
  AND ($sellerFilter IS NULL OR r.seller_tax_code = $sellerFilter)
  AND r.receipt_date >= $fromLiteral AND r.receipt_date <= $toLiteral
  AND COALESCE(ra.allocated, 0) > 0;
"@
  }
}

$queries += [pscustomobject]@{
  Name = "aging"
  Sql = @"
WITH invoice_alloc AS (
    SELECT ra.invoice_id, SUM(ra.amount) AS allocated
    FROM congno.receipt_allocations ra
    JOIN congno.receipts r ON r.id = ra.receipt_id
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND r.receipt_date <= $asOfLiteral
    GROUP BY ra.invoice_id
),
advance_alloc AS (
    SELECT ra.advance_id, SUM(ra.amount) AS allocated
    FROM congno.receipt_allocations ra
    JOIN congno.receipts r ON r.id = ra.receipt_id
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND r.receipt_date <= $asOfLiteral
    GROUP BY ra.advance_id
),
invoice_out AS (
    SELECT i.customer_tax_code,
           c.name AS customer_name,
           i.seller_tax_code,
           (i.total_amount - COALESCE(a.allocated, 0)) AS outstanding,
           (i.issue_date + (COALESCE(c.payment_terms_days, 0) || ' days')::interval)::date AS due_date
    FROM congno.invoices i
    JOIN congno.customers c ON c.tax_code = i.customer_tax_code
    LEFT JOIN invoice_alloc a ON a.invoice_id = i.id
    WHERE i.deleted_at IS NULL
      AND ($sellerFilter IS NULL OR i.seller_tax_code = $sellerFilter)
      AND ($customerFilter IS NULL OR i.customer_tax_code = $customerFilter)
      AND ($ownerLiteral IS NULL OR c.accountant_owner_id = $ownerLiteral)
),
advance_out AS (
    SELECT a.customer_tax_code,
           c.name AS customer_name,
           a.seller_tax_code,
           (a.amount - COALESCE(alloc.allocated, 0)) AS outstanding,
           (a.advance_date + (COALESCE(c.payment_terms_days, 0) || ' days')::interval)::date AS due_date
    FROM congno.advances a
    JOIN congno.customers c ON c.tax_code = a.customer_tax_code
    LEFT JOIN advance_alloc alloc ON alloc.advance_id = a.id
    WHERE a.deleted_at IS NULL
      AND a.status IN ('APPROVED','PAID')
      AND ($sellerFilter IS NULL OR a.seller_tax_code = $sellerFilter)
      AND ($customerFilter IS NULL OR a.customer_tax_code = $customerFilter)
      AND ($ownerLiteral IS NULL OR c.accountant_owner_id = $ownerLiteral)
),
combined AS (
    SELECT * FROM invoice_out
    UNION ALL
    SELECT * FROM advance_out
),
bucketed AS (
    SELECT customer_tax_code,
           customer_name,
           seller_tax_code,
           outstanding,
           GREATEST(0, ($asOfLiteral - due_date))::int AS days_past_due
    FROM combined
    WHERE outstanding > 0
)
SELECT customer_tax_code AS customer_tax_code,
       customer_name AS customer_name,
       seller_tax_code AS seller_tax_code,
       SUM(CASE WHEN days_past_due <= 30 THEN outstanding ELSE 0 END) AS bucket0to30,
       SUM(CASE WHEN days_past_due BETWEEN 31 AND 60 THEN outstanding ELSE 0 END) AS bucket31to60,
       SUM(CASE WHEN days_past_due BETWEEN 61 AND 90 THEN outstanding ELSE 0 END) AS bucket61to90,
       SUM(CASE WHEN days_past_due BETWEEN 91 AND 180 THEN outstanding ELSE 0 END) AS bucket91to180,
       SUM(CASE WHEN days_past_due > 180 THEN outstanding ELSE 0 END) AS bucketover180,
       SUM(outstanding) AS total,
       SUM(CASE WHEN days_past_due > 0 THEN outstanding ELSE 0 END) AS overdue
FROM bucketed
GROUP BY customer_tax_code, customer_name, seller_tax_code
ORDER BY customer_name;
"@
}

$output = New-Object System.Collections.Generic.List[string]
$summary = @{}

foreach ($q in $queries) {
  $output.Add("=== $($q.Name) ===")
  $cmd = $conn.CreateCommand()
  $cmd.CommandText = "EXPLAIN (ANALYZE, BUFFERS) " + $q.Sql
  $reader = $cmd.ExecuteReader()
  while ($reader.Read()) {
    $line = $reader.GetString(0)
    $output.Add($line)
    if ($line -match "Execution Time: ([0-9\\.]+) ms") {
      $summary[$q.Name] = $matches[1]
    }
  }
  $reader.Close()
  $output.Add("")
}

$conn.Close()

$output | Set-Content -Path $OutputPath

Write-Host "Perf report written to $OutputPath"
Write-Host "Execution times (ms):"
foreach ($key in $summary.Keys) {
  Write-Host ("- {0}: {1}" -f $key, $summary[$key])
}
