using CongNoGolden.Application.Reports;
using Dapper;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReportService
{
    private const string StatementOpeningSql = @"
WITH invoices AS (
    SELECT SUM(total_amount) AS total
    FROM congno.invoices
    WHERE deleted_at IS NULL
      AND customer_tax_code = @customerTaxCode
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND issue_date < @from
),
advances AS (
    SELECT SUM(amount) AS total
    FROM congno.advances
    WHERE deleted_at IS NULL
      AND status IN ('APPROVED','PAID')
      AND customer_tax_code = @customerTaxCode
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND advance_date < @from
),
receipts AS (
    SELECT SUM(ra.amount) AS total
    FROM congno.receipt_allocations ra
    JOIN congno.receipts r ON r.id = ra.receipt_id
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND r.customer_tax_code = @customerTaxCode
      AND (@sellerTaxCode IS NULL OR r.seller_tax_code = @sellerTaxCode)
      AND r.receipt_date < @from
)
SELECT COALESCE(i.total, 0) + COALESCE(a.total, 0) - COALESCE(r.total, 0) AS opening_balance
FROM invoices i, advances a, receipts r;
";

private const string StatementLinesSql = @"
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
  AND (@customerTaxCode IS NULL OR i.customer_tax_code = @customerTaxCode)
  AND (@sellerTaxCode IS NULL OR i.seller_tax_code = @sellerTaxCode)
  AND i.issue_date >= @from AND i.issue_date <= @to

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
  AND (@customerTaxCode IS NULL OR a.customer_tax_code = @customerTaxCode)
  AND (@sellerTaxCode IS NULL OR a.seller_tax_code = @sellerTaxCode)
  AND a.advance_date >= @from AND a.advance_date <= @to

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
  AND (@customerTaxCode IS NULL OR r.customer_tax_code = @customerTaxCode)
  AND (@sellerTaxCode IS NULL OR r.seller_tax_code = @sellerTaxCode)
  AND r.receipt_date >= @from AND r.receipt_date <= @to
  AND COALESCE(ra.allocated, 0) > 0;
";

private const string StatementPagedBaseSql = @"
WITH receipt_alloc AS (
    SELECT ra.receipt_id, SUM(ra.amount) AS allocated
    FROM congno.receipt_allocations ra
    JOIN congno.receipts r ON r.id = ra.receipt_id
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
    GROUP BY ra.receipt_id
),
lines AS (
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
           NULL::text AS created_by,
           NULL::text AS approved_by,
           i.source_batch_id::text AS batch
    FROM congno.invoices i
    JOIN congno.customers c ON c.tax_code = i.customer_tax_code
    WHERE i.deleted_at IS NULL
      AND (@customerTaxCode IS NULL OR i.customer_tax_code = @customerTaxCode)
      AND (@sellerTaxCode IS NULL OR i.seller_tax_code = @sellerTaxCode)
      AND i.issue_date >= @from AND i.issue_date <= @to

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
           COALESCE(u_created.full_name, u_created.username) AS created_by,
           COALESCE(u_approved.full_name, u_approved.username) AS approved_by,
           a.source_batch_id::text AS batch
    FROM congno.advances a
    JOIN congno.customers c ON c.tax_code = a.customer_tax_code
    LEFT JOIN congno.users u_created ON u_created.id = a.created_by
    LEFT JOIN congno.users u_approved ON u_approved.id = a.approved_by
    WHERE a.deleted_at IS NULL
      AND a.status IN ('APPROVED','PAID')
      AND (@customerTaxCode IS NULL OR a.customer_tax_code = @customerTaxCode)
      AND (@sellerTaxCode IS NULL OR a.seller_tax_code = @sellerTaxCode)
      AND a.advance_date >= @from AND a.advance_date <= @to

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
      AND (@customerTaxCode IS NULL OR r.customer_tax_code = @customerTaxCode)
      AND (@sellerTaxCode IS NULL OR r.seller_tax_code = @sellerTaxCode)
      AND r.receipt_date >= @from AND r.receipt_date <= @to
      AND COALESCE(ra.allocated, 0) > 0
),
ordered AS (
    SELECT *,
           CASE UPPER(type)
               WHEN 'INVOICE' THEN 1
               WHEN 'ADVANCE' THEN 2
               WHEN 'RECEIPT' THEN 3
               ELSE 9
           END AS type_order
    FROM lines
),
running AS (
    SELECT *,
           SUM(increase - decrease) OVER (
               PARTITION BY customer_tax_code
               ORDER BY document_date, type_order, document_no
           ) AS running_balance
    FROM ordered
)
";

private const string StatementPagedSelectSql = @"
SELECT document_date AS document_date,
       applied_period_start AS applied_period_start,
       type AS type,
       seller_tax_code AS seller_tax_code,
       customer_tax_code AS customer_tax_code,
       customer_name AS customer_name,
       document_no AS document_no,
       description AS description,
       revenue AS revenue,
       vat AS vat,
       increase AS increase,
       decrease AS decrease,
       running_balance AS running_balance,
       created_by AS created_by,
       approved_by AS approved_by,
       batch AS batch
FROM running
ORDER BY customer_name, document_date, type_order, document_no
OFFSET @offset LIMIT @pageSize;
";

private static string BuildStatementPagedSql()
{
    var countSql = $"{StatementPagedBaseSql}\nSELECT COUNT(*) FROM lines;";
    var totalSql = $"{StatementPagedBaseSql}\nSELECT COALESCE(SUM(increase - decrease), 0) FROM lines;";
    var itemsSql = $"{StatementPagedBaseSql}\n{StatementPagedSelectSql}";
    return $"{countSql}\n{totalSql}\n{itemsSql}";
}

public async Task<ReportStatementResult> GetStatementAsync(ReportStatementRequest request, CancellationToken ct)
{
    var hasCustomer = !string.IsNullOrWhiteSpace(request.CustomerTaxCode);

    var from = request.From ?? new DateOnly(1900, 1, 1);
    var to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);

    var parameters = new
    {
        from,
        to,
        customerTaxCode = hasCustomer ? request.CustomerTaxCode : null,
        sellerTaxCode = request.SellerTaxCode
    };

    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(ct);

    var lines = (await connection.QueryAsync<ReportStatementLine>(
        new CommandDefinition(StatementLinesSql, parameters, cancellationToken: ct))).ToList();

    if (!hasCustomer)
    {
        var grouped = lines
            .GroupBy(l => l.CustomerTaxCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.FirstOrDefault()?.CustomerName ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        var resultLines = new List<ReportStatementLine>(lines.Count);
        foreach (var group in grouped)
        {
            var running = 0m;
            var ordered = group
                .OrderBy(l => l.DocumentDate)
                .ThenBy(l => GetStatementTypeOrder(l.Type))
                .ThenBy(l => l.DocumentNo)
                .ToList();

            foreach (var line in ordered)
            {
                running += line.Increase - line.Decrease;
                line.RunningBalance = running;
                resultLines.Add(line);
            }
        }

        return new ReportStatementResult(0m, 0m, resultLines);
    }

    var opening = await connection.ExecuteScalarAsync<decimal>(
        new CommandDefinition(StatementOpeningSql, parameters, cancellationToken: ct));

    var orderedCustomer = lines
        .OrderBy(l => l.DocumentDate)
        .ThenBy(l => GetStatementTypeOrder(l.Type))
        .ThenBy(l => l.DocumentNo)
        .ToList();

    var runningCustomer = opening;
    var resultCustomerLines = new List<ReportStatementLine>(orderedCustomer.Count);
    foreach (var line in orderedCustomer)
    {
        runningCustomer += line.Increase - line.Decrease;
        line.RunningBalance = runningCustomer;
        resultCustomerLines.Add(line);
    }

    return new ReportStatementResult(opening, runningCustomer, resultCustomerLines);
}

public async Task<ReportStatementPagedResult> GetStatementPagedAsync(
    ReportStatementPagedRequest request,
    CancellationToken ct)
{
    var hasCustomer = !string.IsNullOrWhiteSpace(request.CustomerTaxCode);

    var from = request.From ?? new DateOnly(1900, 1, 1);
    var to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);

    var page = NormalizePage(request.Page);
    var pageSize = NormalizePageSize(request.PageSize);
    var offset = (page - 1) * pageSize;

    var parameters = new
    {
        from,
        to,
        customerTaxCode = hasCustomer ? request.CustomerTaxCode : null,
        sellerTaxCode = request.SellerTaxCode,
        offset,
        pageSize
    };

    await using var connection = _connectionFactory.Create();
    await connection.OpenAsync(ct);

    var sql = BuildStatementPagedSql();

    using var multi = await connection.QueryMultipleAsync(
        new CommandDefinition(sql, parameters, cancellationToken: ct));

    var total = await multi.ReadSingleAsync<int>();
    var totalChange = await multi.ReadSingleAsync<decimal>();
    var lines = (await multi.ReadAsync<ReportStatementLine>()).ToList();

    if (!hasCustomer)
    {
        return new ReportStatementPagedResult(0m, 0m, lines, page, pageSize, total);
    }

    var opening = await connection.ExecuteScalarAsync<decimal>(
        new CommandDefinition(StatementOpeningSql, parameters, cancellationToken: ct));

    foreach (var line in lines)
    {
        line.RunningBalance += opening;
    }

    var closing = opening + totalChange;

    return new ReportStatementPagedResult(opening, closing, lines, page, pageSize, total);
}

    private static int GetStatementTypeOrder(string type)
    {
        return type.ToUpperInvariant() switch
        {
            "INVOICE" => 1,
            "ADVANCE" => 2,
            "RECEIPT" => 3,
            _ => 9
        };
    }
}
