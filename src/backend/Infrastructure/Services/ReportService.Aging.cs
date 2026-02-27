using System.Data;
using CongNoGolden.Application.Reports;
using Dapper;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReportService
{
    private const string AgingSql = @"
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
           i.seller_tax_code,
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
           a.seller_tax_code,
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
    SELECT * FROM invoice_out
    UNION ALL
    SELECT * FROM advance_out
),
bucketed AS (
    SELECT customer_tax_code,
           customer_name,
           seller_tax_code,
           outstanding,
           GREATEST(0, (@asOf::date - due_date))::int AS days_past_due
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
";

    private const string AgingPagedBaseSql = @"
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
           i.seller_tax_code,
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
           a.seller_tax_code,
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
    SELECT * FROM invoice_out
    UNION ALL
    SELECT * FROM advance_out
),
bucketed AS (
    SELECT customer_tax_code,
           customer_name,
           seller_tax_code,
           outstanding,
           GREATEST(0, (@asOf::date - due_date))::int AS days_past_due
    FROM combined
    WHERE outstanding > 0
),
base AS (
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
)
SELECT * FROM base
";

    private static string ResolveAgingOrder(string? sortKey, string? sortDirection)
    {
        var normalized = NormalizeSortKey(sortKey);
        var direction = NormalizeSortDirection(sortDirection);
        var column = normalized switch
        {
            "overdue" => "overdue",
            "bucket0to30" => "bucket0to30",
            "bucket31to60" => "bucket31to60",
            "bucket61to90" => "bucket61to90",
            "bucket91to180" => "bucket91to180",
            "bucketover180" => "bucketover180",
            "over180" => "bucketover180",
            _ => string.Empty
        };

        if (!string.IsNullOrEmpty(column))
        {
            return $"{column} {direction}, customer_name ASC";
        }

        return "customer_name ASC";
    }

    public async Task<IReadOnlyList<ReportAgingRow>> GetAgingAsync(ReportAgingRequest request, CancellationToken ct)
    {
        var asOf = request.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var parameters = new
        {
            asOf,
            sellerTaxCode = request.SellerTaxCode,
            customerTaxCode = request.CustomerTaxCode,
            ownerId = request.OwnerId
        };

        await using var connection = _connectionFactory.CreateRead();
        await connection.OpenAsync(ct);

        var rows = await connection.QueryAsync<ReportAgingRow>(
            new CommandDefinition(AgingSql, parameters, cancellationToken: ct));

        return rows.ToList();
    }

    public async Task<PagedResult<ReportAgingRow>> GetAgingPagedAsync(
        ReportAgingPagedRequest request,
        CancellationToken ct)
    {
        var asOf = request.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var offset = (page - 1) * pageSize;
        var orderBy = ResolveAgingOrder(request.SortKey, request.SortDirection);
        var sql = BuildPagedSql(AgingPagedBaseSql, orderBy);

        var parameters = new DynamicParameters();
        parameters.Add("asOf", asOf, DbType.Date);
        parameters.Add("sellerTaxCode", request.SellerTaxCode, DbType.String);
        parameters.Add("customerTaxCode", request.CustomerTaxCode, DbType.String);
        parameters.Add("ownerId", request.OwnerId, DbType.Guid);
        parameters.Add("offset", offset, DbType.Int32);
        parameters.Add("pageSize", pageSize, DbType.Int32);

        await using var connection = _connectionFactory.CreateRead();
        await connection.OpenAsync(ct);

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<ReportAgingRow>()).ToList();

        return new PagedResult<ReportAgingRow>(items, page, pageSize, total);
    }
}
