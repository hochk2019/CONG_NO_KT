using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Risk;
using CongNoGolden.Domain.Risk;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Services.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class RiskService : IRiskService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ConGNoDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly IRiskAiModelService _riskAiModelService;

    public RiskService(
        IDbConnectionFactory connectionFactory,
        ConGNoDbContext db,
        ICurrentUser currentUser,
        IAuditService auditService,
        IRiskAiModelService riskAiModelService)
    {
        _connectionFactory = connectionFactory;
        _db = db;
        _currentUser = currentUser;
        _auditService = auditService;
        _riskAiModelService = riskAiModelService;
    }

    public async Task<RiskOverviewDto> GetOverviewAsync(RiskOverviewRequest request, CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var ownerId = _currentUser.ResolveOwnerFilter();
        var asOf = request.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);

        await using var connection = _connectionFactory.CreateRead();
        await connection.OpenAsync(ct);

        var rows = await connection.QueryAsync<RiskOverviewRow>(
            new CommandDefinition(RiskOverviewSql, new { ownerId, asOf }, cancellationToken: ct));

        var list = rows.ToList();
        var totalCustomers = list.Sum(r => r.Customers);
        var totalOutstanding = list.Sum(r => r.TotalOutstanding);
        var totalOverdue = list.Sum(r => r.OverdueAmount);

        var items = list
            .OrderByDescending(r => r.RiskRank)
            .Select(r => new RiskOverviewItem(
                r.Level ?? "LOW",
                r.Customers,
                r.TotalOutstanding,
                r.OverdueAmount))
            .ToList();

        return new RiskOverviewDto(asOf, items, totalCustomers, totalOutstanding, totalOverdue);
    }

    public async Task<PagedResult<RiskCustomerItem>> ListCustomersAsync(RiskCustomerListRequest request, CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var ownerId = _currentUser.ResolveOwnerFilter(request.OwnerId);
        var asOf = request.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 5 or > 200 ? 20 : request.PageSize;
        var offset = (page - 1) * pageSize;

        var level = NormalizeLevel(request.Level);
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search.Trim()}%";

        var sortColumn = ResolveSortColumn(request.Sort);
        var sortDirection = ResolveSortDirection(request.Order);
        var orderBy = $"{sortColumn} {sortDirection}, overdue_amount DESC";

        var sql = string.Format(RiskListSqlTemplate, RiskBaseCte, orderBy);

        await using var connection = _connectionFactory.CreateRead();
        await connection.OpenAsync(ct);

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                ownerId,
                asOf,
                level,
                search,
                limit = pageSize,
                offset
            }, cancellationToken: ct));

        var total = await multi.ReadSingleAsync<int>();
        var rows = (await multi.ReadAsync<RiskCustomerRow>()).ToList();
        await _riskAiModelService.GetActiveModelAsync(modelKey: null, ct);

        var items = rows
            .Select(r =>
            {
                var prediction = _riskAiModelService.Predict(new RiskMetrics(
                    r.TotalOutstanding,
                    r.OverdueAmount,
                    r.OverdueRatio,
                    r.MaxDaysPastDue,
                    r.LateCount), asOf);

                return new RiskCustomerItem(
                    r.CustomerTaxCode ?? string.Empty,
                    r.CustomerName ?? r.CustomerTaxCode ?? string.Empty,
                    r.OwnerId,
                    r.OwnerName,
                    r.TotalOutstanding,
                    r.OverdueAmount,
                    r.OverdueRatio,
                    r.MaxDaysPastDue,
                    r.LateCount,
                    r.RiskLevel ?? "LOW",
                    prediction.Probability,
                    prediction.Signal,
                    prediction.Factors
                        .Select(factor => new RiskAiFactorItem(
                            factor.Code,
                            factor.Label,
                            factor.RawValue,
                            factor.NormalizedValue,
                            factor.Weight,
                            factor.Contribution))
                        .ToList(),
                    prediction.Recommendation);
            })
            .ToList();

        return new PagedResult<RiskCustomerItem>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<RiskRuleDto>> GetRulesAsync(CancellationToken ct)
    {
        var rules = await _db.RiskRules.AsNoTracking()
            .OrderBy(r => r.SortOrder)
            .Select(r => new RiskRuleDto(
                r.Level,
                r.MinOverdueDays,
                r.MinOverdueRatio,
                r.MinLateCount,
                r.IsActive,
                r.MatchMode))
            .ToListAsync(ct);

        return rules;
    }

    public async Task UpdateRulesAsync(RiskRulesUpdateRequest request, CancellationToken ct)
    {
        _currentUser.EnsureUser();

        if (request.Rules.Count == 0)
        {
            throw new InvalidOperationException("Risk rules are required.");
        }

        var normalized = request.Rules
            .Select((rule, index) => new
            {
                Level = NormalizeLevel(rule.Level) ?? string.Empty,
                MatchMode = NormalizeMatchMode(rule.MatchMode),
                rule.MinOverdueDays,
                rule.MinOverdueRatio,
                rule.MinLateCount,
                rule.IsActive,
                SortOrder = index + 1
            })
            .ToList();

        if (normalized.Any(r => string.IsNullOrWhiteSpace(r.Level)))
        {
            throw new InvalidOperationException("Invalid risk level.");
        }

        if (normalized.Any(r => string.IsNullOrWhiteSpace(r.MatchMode)))
        {
            throw new InvalidOperationException("Invalid match mode.");
        }

        if (normalized.Any(r => r.MinOverdueDays < 0 || r.MinLateCount < 0))
        {
            throw new InvalidOperationException("Thresholds must be non-negative.");
        }

        if (normalized.Any(r => r.MinOverdueRatio < 0 || r.MinOverdueRatio > 1))
        {
            throw new InvalidOperationException("Overdue ratio must be between 0 and 1.");
        }

        var existing = await _db.RiskRules.ToListAsync(ct);
        var before = existing.Select(r => new
        {
            r.Level,
            r.MatchMode,
            r.MinOverdueDays,
            r.MinOverdueRatio,
            r.MinLateCount,
            r.IsActive,
            r.SortOrder
        });

        foreach (var rule in normalized)
        {
            var entity = existing.FirstOrDefault(r =>
                string.Equals(r.Level, rule.Level, StringComparison.OrdinalIgnoreCase));
            if (entity is null)
            {
                entity = new Infrastructure.Data.Entities.RiskRule
                {
                    Id = Guid.NewGuid(),
                    Level = rule.Level
                };
                _db.RiskRules.Add(entity);
                existing.Add(entity);
            }

            entity.MatchMode = rule.MatchMode!;
            entity.MinOverdueDays = rule.MinOverdueDays;
            entity.MinOverdueRatio = rule.MinOverdueRatio;
            entity.MinLateCount = rule.MinLateCount;
            entity.IsActive = rule.IsActive;
            entity.SortOrder = rule.SortOrder;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        var after = normalized.Select(r => new
        {
            r.Level,
            r.MatchMode,
            r.MinOverdueDays,
            r.MinOverdueRatio,
            r.MinLateCount,
            r.IsActive,
            r.SortOrder
        });

        await _auditService.LogAsync(
            "RISK_RULES_UPDATE",
            "RiskRule",
            "ALL",
            before,
            after,
            ct);
    }

    private static string? NormalizeLevel(string? level)
    {
        if (!RiskLevelExtensions.TryParse(level, out var parsed))
        {
            return null;
        }

        return parsed.ToCode();
    }

    private static string? NormalizeMatchMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return RiskMatchMode.Any.ToCode();
        }

        return RiskMatchModeExtensions.TryParse(mode, out var parsed)
            ? parsed.ToCode()
            : null;
    }

    private static string ResolveSortColumn(string? sort)
    {
        return sort?.Trim() switch
        {
            "customerName" => "customer_name",
            "totalOutstanding" => "total_outstanding",
            "overdueAmount" => "overdue_amount",
            "overdueRatio" => "overdue_ratio",
            "maxDaysPastDue" => "max_days_past_due",
            "lateCount" => "late_count",
            "riskLevel" => "risk_rank",
            _ => "risk_rank"
        };
    }

    private static string ResolveSortDirection(string? order)
    {
        return string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
    }

    private sealed class RiskOverviewRow
    {
        public string? Level { get; set; }
        public int Customers { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal OverdueAmount { get; set; }
        public int RiskRank { get; set; }
    }

    private sealed class RiskCustomerRow
    {
        public string? CustomerTaxCode { get; set; }
        public string? CustomerName { get; set; }
        public Guid? OwnerId { get; set; }
        public string? OwnerName { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal OverdueAmount { get; set; }
        public decimal OverdueRatio { get; set; }
        public int MaxDaysPastDue { get; set; }
        public int LateCount { get; set; }
        public string? RiskLevel { get; set; }
    }
}
