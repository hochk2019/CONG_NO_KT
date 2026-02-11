using System.Data;
using CongNoGolden.Application.Reports;
using Dapper;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReportService
{
    private const string SummaryByCustomerSql = @"
WITH invoice AS (
    SELECT customer_tax_code AS key,
           SUM(total_amount) AS invoiced_total,
           SUM(outstanding_amount) AS outstanding_invoice
    FROM congno.invoices
    WHERE deleted_at IS NULL
      AND (@from IS NULL OR issue_date >= @from)
      AND (@to IS NULL OR issue_date <= @to)
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR customer_tax_code = @customerTaxCode)
    GROUP BY customer_tax_code
),
advance AS (
    SELECT customer_tax_code AS key,
           SUM(amount) AS advanced_total,
           SUM(outstanding_amount) AS outstanding_advance
    FROM congno.advances
    WHERE deleted_at IS NULL
      AND (@from IS NULL OR advance_date >= @from)
      AND (@to IS NULL OR advance_date <= @to)
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR customer_tax_code = @customerTaxCode)
    GROUP BY customer_tax_code
),
receipt AS (
    SELECT customer_tax_code AS key,
           SUM(amount) AS receipted_total
    FROM congno.receipts
    WHERE deleted_at IS NULL
      AND status = 'APPROVED'
      AND (@from IS NULL OR receipt_date >= @from)
      AND (@to IS NULL OR receipt_date <= @to)
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR customer_tax_code = @customerTaxCode)
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
WHERE (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
  AND (@customerTaxCode IS NULL OR c.tax_code = @customerTaxCode)
ORDER BY c.name;
";

    private const string SummaryByCustomerPagedBaseSql = @"
WITH invoice AS (
    SELECT customer_tax_code AS key,
           SUM(total_amount) AS invoiced_total,
           SUM(outstanding_amount) AS outstanding_invoice
    FROM congno.invoices
    WHERE deleted_at IS NULL
      AND (@from IS NULL OR issue_date >= @from)
      AND (@to IS NULL OR issue_date <= @to)
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR customer_tax_code = @customerTaxCode)
    GROUP BY customer_tax_code
),
advance AS (
    SELECT customer_tax_code AS key,
           SUM(amount) AS advanced_total,
           SUM(outstanding_amount) AS outstanding_advance
    FROM congno.advances
    WHERE deleted_at IS NULL
      AND (@from IS NULL OR advance_date >= @from)
      AND (@to IS NULL OR advance_date <= @to)
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR customer_tax_code = @customerTaxCode)
    GROUP BY customer_tax_code
),
receipt AS (
    SELECT customer_tax_code AS key,
           SUM(amount) AS receipted_total
    FROM congno.receipts
    WHERE deleted_at IS NULL
      AND status = 'APPROVED'
      AND (@from IS NULL OR receipt_date >= @from)
      AND (@to IS NULL OR receipt_date <= @to)
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR customer_tax_code = @customerTaxCode)
    GROUP BY customer_tax_code
),
base AS (
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
    WHERE (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
      AND (@customerTaxCode IS NULL OR c.tax_code = @customerTaxCode)
)
SELECT * FROM base
";

    private const string SummaryBySellerSql = @"
WITH invoice AS (
    SELECT seller_tax_code AS key,
           SUM(total_amount) AS invoiced_total,
           SUM(outstanding_amount) AS outstanding_invoice
    FROM congno.invoices
    WHERE deleted_at IS NULL
      AND (@from IS NULL OR issue_date >= @from)
      AND (@to IS NULL OR issue_date <= @to)
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR customer_tax_code = @customerTaxCode)
    GROUP BY seller_tax_code
),
advance AS (
    SELECT seller_tax_code AS key,
           SUM(amount) AS advanced_total,
           SUM(outstanding_amount) AS outstanding_advance
    FROM congno.advances
    WHERE deleted_at IS NULL
      AND (@from IS NULL OR advance_date >= @from)
      AND (@to IS NULL OR advance_date <= @to)
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR customer_tax_code = @customerTaxCode)
    GROUP BY seller_tax_code
),
receipt AS (
    SELECT seller_tax_code AS key,
           SUM(amount) AS receipted_total
    FROM congno.receipts
    WHERE deleted_at IS NULL
      AND status = 'APPROVED'
      AND (@from IS NULL OR receipt_date >= @from)
      AND (@to IS NULL OR receipt_date <= @to)
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR customer_tax_code = @customerTaxCode)
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
WHERE (@sellerTaxCode IS NULL OR s.seller_tax_code = @sellerTaxCode)
ORDER BY s.name;
";

    private const string SummaryBySellerPagedBaseSql = @"
WITH invoice AS (
    SELECT seller_tax_code AS key,
           SUM(total_amount) AS invoiced_total,
           SUM(outstanding_amount) AS outstanding_invoice
    FROM congno.invoices
    WHERE deleted_at IS NULL
      AND (@from IS NULL OR issue_date >= @from)
      AND (@to IS NULL OR issue_date <= @to)
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR customer_tax_code = @customerTaxCode)
    GROUP BY seller_tax_code
),
advance AS (
    SELECT seller_tax_code AS key,
           SUM(amount) AS advanced_total,
           SUM(outstanding_amount) AS outstanding_advance
    FROM congno.advances
    WHERE deleted_at IS NULL
      AND (@from IS NULL OR advance_date >= @from)
      AND (@to IS NULL OR advance_date <= @to)
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR customer_tax_code = @customerTaxCode)
    GROUP BY seller_tax_code
),
receipt AS (
    SELECT seller_tax_code AS key,
           SUM(amount) AS receipted_total
    FROM congno.receipts
    WHERE deleted_at IS NULL
      AND status = 'APPROVED'
      AND (@from IS NULL OR receipt_date >= @from)
      AND (@to IS NULL OR receipt_date <= @to)
      AND (@sellerTaxCode IS NULL OR seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR customer_tax_code = @customerTaxCode)
    GROUP BY seller_tax_code
),
base AS (
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
    WHERE (@sellerTaxCode IS NULL OR s.seller_tax_code = @sellerTaxCode)
)
SELECT * FROM base
";

    private const string SummaryByOwnerSql = @"
WITH invoice AS (
    SELECT COALESCE(c.accountant_owner_id, '00000000-0000-0000-0000-000000000000'::uuid) AS key,
           SUM(i.total_amount) AS invoiced_total,
           SUM(i.outstanding_amount) AS outstanding_invoice
    FROM congno.invoices i
    JOIN congno.customers c ON c.tax_code = i.customer_tax_code
    WHERE i.deleted_at IS NULL
      AND (@from IS NULL OR i.issue_date >= @from)
      AND (@to IS NULL OR i.issue_date <= @to)
      AND (@sellerTaxCode IS NULL OR i.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR i.customer_tax_code = @customerTaxCode)
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    GROUP BY COALESCE(c.accountant_owner_id, '00000000-0000-0000-0000-000000000000'::uuid)
),
advance AS (
    SELECT COALESCE(c.accountant_owner_id, '00000000-0000-0000-0000-000000000000'::uuid) AS key,
           SUM(a.amount) AS advanced_total,
           SUM(a.outstanding_amount) AS outstanding_advance
    FROM congno.advances a
    JOIN congno.customers c ON c.tax_code = a.customer_tax_code
    WHERE a.deleted_at IS NULL
      AND (@from IS NULL OR a.advance_date >= @from)
      AND (@to IS NULL OR a.advance_date <= @to)
      AND (@sellerTaxCode IS NULL OR a.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR a.customer_tax_code = @customerTaxCode)
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    GROUP BY COALESCE(c.accountant_owner_id, '00000000-0000-0000-0000-000000000000'::uuid)
),
receipt AS (
    SELECT COALESCE(c.accountant_owner_id, '00000000-0000-0000-0000-000000000000'::uuid) AS key,
           SUM(r.amount) AS receipted_total
    FROM congno.receipts r
    JOIN congno.customers c ON c.tax_code = r.customer_tax_code
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND (@from IS NULL OR r.receipt_date >= @from)
      AND (@to IS NULL OR r.receipt_date <= @to)
      AND (@sellerTaxCode IS NULL OR r.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR r.customer_tax_code = @customerTaxCode)
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    GROUP BY COALESCE(c.accountant_owner_id, '00000000-0000-0000-0000-000000000000'::uuid)
),
owners AS (
    SELECT DISTINCT COALESCE(accountant_owner_id, '00000000-0000-0000-0000-000000000000'::uuid) AS key
    FROM congno.customers
)
SELECT CASE WHEN o.key = '00000000-0000-0000-0000-000000000000'::uuid THEN 'UNASSIGNED' ELSE o.key::text END AS group_key,
       CASE WHEN o.key = '00000000-0000-0000-0000-000000000000'::uuid THEN 'Unassigned' ELSE COALESCE(u.username, u.full_name, 'Unknown') END AS group_name,
       COALESCE(i.invoiced_total, 0) AS invoiced_total,
       COALESCE(a.advanced_total, 0) AS advanced_total,
       COALESCE(r.receipted_total, 0) AS receipted_total,
       COALESCE(i.outstanding_invoice, 0) AS outstanding_invoice,
       COALESCE(a.outstanding_advance, 0) AS outstanding_advance,
       0 AS current_balance
FROM owners o
LEFT JOIN congno.users u ON u.id = o.key
LEFT JOIN invoice i ON i.key = o.key
LEFT JOIN advance a ON a.key = o.key
LEFT JOIN receipt r ON r.key = o.key
WHERE (@ownerId IS NULL OR o.key = @ownerId)
ORDER BY group_name;
";

    private const string SummaryByOwnerPagedBaseSql = @"
WITH invoice AS (
    SELECT COALESCE(c.accountant_owner_id, '00000000-0000-0000-0000-000000000000'::uuid) AS key,
           SUM(i.total_amount) AS invoiced_total,
           SUM(i.outstanding_amount) AS outstanding_invoice
    FROM congno.invoices i
    JOIN congno.customers c ON c.tax_code = i.customer_tax_code
    WHERE i.deleted_at IS NULL
      AND (@from IS NULL OR i.issue_date >= @from)
      AND (@to IS NULL OR i.issue_date <= @to)
      AND (@sellerTaxCode IS NULL OR i.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR i.customer_tax_code = @customerTaxCode)
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    GROUP BY COALESCE(c.accountant_owner_id, '00000000-0000-0000-0000-000000000000'::uuid)
),
advance AS (
    SELECT COALESCE(c.accountant_owner_id, '00000000-0000-0000-0000-000000000000'::uuid) AS key,
           SUM(a.amount) AS advanced_total,
           SUM(a.outstanding_amount) AS outstanding_advance
    FROM congno.advances a
    JOIN congno.customers c ON c.tax_code = a.customer_tax_code
    WHERE a.deleted_at IS NULL
      AND (@from IS NULL OR a.advance_date >= @from)
      AND (@to IS NULL OR a.advance_date <= @to)
      AND (@sellerTaxCode IS NULL OR a.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR a.customer_tax_code = @customerTaxCode)
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    GROUP BY COALESCE(c.accountant_owner_id, '00000000-0000-0000-0000-000000000000'::uuid)
),
receipt AS (
    SELECT COALESCE(c.accountant_owner_id, '00000000-0000-0000-0000-000000000000'::uuid) AS key,
           SUM(r.amount) AS receipted_total
    FROM congno.receipts r
    JOIN congno.customers c ON c.tax_code = r.customer_tax_code
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND (@from IS NULL OR r.receipt_date >= @from)
      AND (@to IS NULL OR r.receipt_date <= @to)
      AND (@sellerTaxCode IS NULL OR r.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR r.customer_tax_code = @customerTaxCode)
      AND (@ownerId IS NULL OR c.accountant_owner_id = @ownerId)
    GROUP BY COALESCE(c.accountant_owner_id, '00000000-0000-0000-0000-000000000000'::uuid)
),
owners AS (
    SELECT DISTINCT COALESCE(accountant_owner_id, '00000000-0000-0000-0000-000000000000'::uuid) AS key
    FROM congno.customers
),
base AS (
    SELECT CASE WHEN o.key = '00000000-0000-0000-0000-000000000000'::uuid THEN 'UNASSIGNED' ELSE o.key::text END AS group_key,
           CASE WHEN o.key = '00000000-0000-0000-0000-000000000000'::uuid THEN 'Unassigned' ELSE COALESCE(u.username, u.full_name, 'Unknown') END AS group_name,
           COALESCE(i.invoiced_total, 0) AS invoiced_total,
           COALESCE(a.advanced_total, 0) AS advanced_total,
           COALESCE(r.receipted_total, 0) AS receipted_total,
           COALESCE(i.outstanding_invoice, 0) AS outstanding_invoice,
           COALESCE(a.outstanding_advance, 0) AS outstanding_advance,
           0 AS current_balance
    FROM owners o
    LEFT JOIN congno.users u ON u.id = o.key
    LEFT JOIN invoice i ON i.key = o.key
    LEFT JOIN advance a ON a.key = o.key
    LEFT JOIN receipt r ON r.key = o.key
    WHERE (@ownerId IS NULL OR o.key = @ownerId)
)
SELECT * FROM base
";

    private const string SummaryByPeriodSql = @"
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
      AND (@from IS NULL OR i.issue_date >= @from)
      AND (@to IS NULL OR i.issue_date <= @to)
      AND (@sellerTaxCode IS NULL OR i.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR i.customer_tax_code = @customerTaxCode)
      AND (@ownerId IS NULL OR EXISTS (
          SELECT 1 FROM congno.customers c
          WHERE c.tax_code = i.customer_tax_code AND c.accountant_owner_id = @ownerId
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
      AND (@from IS NULL OR a.advance_date >= @from)
      AND (@to IS NULL OR a.advance_date <= @to)
      AND (@sellerTaxCode IS NULL OR a.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR a.customer_tax_code = @customerTaxCode)
      AND (@ownerId IS NULL OR EXISTS (
          SELECT 1 FROM congno.customers c
          WHERE c.tax_code = a.customer_tax_code AND c.accountant_owner_id = @ownerId
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
      AND (@from IS NULL OR r.receipt_date >= @from)
      AND (@to IS NULL OR r.receipt_date <= @to)
      AND (@sellerTaxCode IS NULL OR r.seller_tax_code = @sellerTaxCode)
      AND (@customerTaxCode IS NULL OR r.customer_tax_code = @customerTaxCode)
      AND (@ownerId IS NULL OR EXISTS (
          SELECT 1 FROM congno.customers c
          WHERE c.tax_code = r.customer_tax_code AND c.accountant_owner_id = @ownerId
      ))
    GROUP BY to_char(r.receipt_date, 'YYYY-MM')
) s
GROUP BY period_key
ORDER BY period_key;
";

    private const string SummaryByPeriodPagedBaseSql = @"
WITH base AS (
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
          AND (@from IS NULL OR i.issue_date >= @from)
          AND (@to IS NULL OR i.issue_date <= @to)
          AND (@sellerTaxCode IS NULL OR i.seller_tax_code = @sellerTaxCode)
          AND (@customerTaxCode IS NULL OR i.customer_tax_code = @customerTaxCode)
          AND (@ownerId IS NULL OR EXISTS (
              SELECT 1 FROM congno.customers c
              WHERE c.tax_code = i.customer_tax_code AND c.accountant_owner_id = @ownerId
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
          AND (@from IS NULL OR a.advance_date >= @from)
          AND (@to IS NULL OR a.advance_date <= @to)
          AND (@sellerTaxCode IS NULL OR a.seller_tax_code = @sellerTaxCode)
          AND (@customerTaxCode IS NULL OR a.customer_tax_code = @customerTaxCode)
          AND (@ownerId IS NULL OR EXISTS (
              SELECT 1 FROM congno.customers c
              WHERE c.tax_code = a.customer_tax_code AND c.accountant_owner_id = @ownerId
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
          AND (@from IS NULL OR r.receipt_date >= @from)
          AND (@to IS NULL OR r.receipt_date <= @to)
          AND (@sellerTaxCode IS NULL OR r.seller_tax_code = @sellerTaxCode)
          AND (@customerTaxCode IS NULL OR r.customer_tax_code = @customerTaxCode)
          AND (@ownerId IS NULL OR EXISTS (
              SELECT 1 FROM congno.customers c
              WHERE c.tax_code = r.customer_tax_code AND c.accountant_owner_id = @ownerId
          ))
        GROUP BY to_char(r.receipt_date, 'YYYY-MM')
    ) s
    GROUP BY period_key
)
SELECT * FROM base
";

    private static string BuildPagedSql(string baseSql, string orderBy)
    {
        var countSql = $"SELECT COUNT(*) FROM ({baseSql}) AS base;";
        var itemsSql =
            $"SELECT * FROM ({baseSql}) AS base ORDER BY {orderBy} OFFSET @offset LIMIT @pageSize;";
        return $"{countSql}\n{itemsSql}";
    }

    private static string ResolveSummaryOrder(string? sortKey, string? sortDirection)
    {
        var normalized = NormalizeSortKey(sortKey);
        if (normalized == "currentbalance")
        {
            var direction = NormalizeSortDirection(sortDirection);
            return $"current_balance {direction}, group_name ASC";
        }

        return "group_name ASC";
    }

    public async Task<IReadOnlyList<ReportSummaryRow>> GetSummaryAsync(ReportSummaryRequest request, CancellationToken ct)
    {
        var groupBy = (request.GroupBy ?? "customer").Trim().ToLowerInvariant();
        var sql = groupBy switch
        {
            "seller" => SummaryBySellerSql,
            "owner" => SummaryByOwnerSql,
            "period" => SummaryByPeriodSql,
            _ => SummaryByCustomerSql
        };

        var parameters = new
        {
            from = request.From,
            to = request.To,
            sellerTaxCode = request.SellerTaxCode,
            customerTaxCode = request.CustomerTaxCode,
            ownerId = request.OwnerId
        };

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        var rows = await connection.QueryAsync<ReportSummaryRow>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        return rows.ToList();
    }

    public async Task<PagedResult<ReportSummaryRow>> GetSummaryPagedAsync(
        ReportSummaryPagedRequest request,
        CancellationToken ct)
    {
        var groupBy = (request.GroupBy ?? "customer").Trim().ToLowerInvariant();
        var baseSql = groupBy switch
        {
            "seller" => SummaryBySellerPagedBaseSql,
            "owner" => SummaryByOwnerPagedBaseSql,
            "period" => SummaryByPeriodPagedBaseSql,
            _ => SummaryByCustomerPagedBaseSql
        };

        var orderBy = ResolveSummaryOrder(request.SortKey, request.SortDirection);
        var sql = BuildPagedSql(baseSql, orderBy);

        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var offset = (page - 1) * pageSize;

        var parameters = new DynamicParameters();
        parameters.Add("from", request.From, DbType.Date);
        parameters.Add("to", request.To, DbType.Date);
        parameters.Add("sellerTaxCode", request.SellerTaxCode, DbType.String);
        parameters.Add("customerTaxCode", request.CustomerTaxCode, DbType.String);
        parameters.Add("ownerId", request.OwnerId, DbType.Guid);
        parameters.Add("offset", offset, DbType.Int32);
        parameters.Add("pageSize", pageSize, DbType.Int32);

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<ReportSummaryRow>()).ToList();

        return new PagedResult<ReportSummaryRow>(items, page, pageSize, total);
    }
}
