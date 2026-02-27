using System.Text.Json;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.Risk;
using CongNoGolden.Domain.Risk;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class RiskService
{
    private const string RiskSnapshotListSql = RiskBaseCte + @"
SELECT customer_tax_code AS customerTaxCode,
       customer_name AS customerName,
       owner_id AS ownerId,
       owner_name AS ownerName,
       total_outstanding AS totalOutstanding,
       overdue_amount AS overdueAmount,
       overdue_ratio AS overdueRatio,
       max_days_past_due AS maxDaysPastDue,
       late_count AS lateCount
FROM classified;";

    public async Task<PagedResult<RiskDeltaAlertItem>> ListDeltaAlertsAsync(
        RiskDeltaAlertListRequest request,
        CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 5 or > 200 ? 20 : request.PageSize;
        var status = NormalizeAlertStatus(request.Status);
        var customerTaxCode = string.IsNullOrWhiteSpace(request.CustomerTaxCode)
            ? null
            : request.CustomerTaxCode.Trim();

        var query = _db.RiskDeltaAlerts.AsNoTracking()
            .Where(x =>
                (status == null || x.Status == status) &&
                (customerTaxCode == null || x.CustomerTaxCode == customerTaxCode) &&
                (request.FromDate == null || x.AsOfDate >= request.FromDate) &&
                (request.ToDate == null || x.AsOfDate <= request.ToDate));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.DetectedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RiskDeltaAlertItem(
                x.Id,
                x.CustomerTaxCode,
                x.AsOfDate,
                x.PrevScore,
                x.CurrScore,
                x.Delta,
                x.Threshold,
                x.Status,
                x.DetectedAt,
                x.ResolvedAt,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResult<RiskDeltaAlertItem>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<RiskScoreHistoryPoint>> GetScoreHistoryAsync(
        string customerTaxCode,
        DateOnly? fromDate,
        DateOnly? toDate,
        int take,
        CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var normalizedTaxCode = customerTaxCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTaxCode))
        {
            return Array.Empty<RiskScoreHistoryPoint>();
        }

        var boundedTake = take is < 1 or > 365 ? 90 : take;
        var rows = await _db.RiskScoreSnapshots.AsNoTracking()
            .Where(x =>
                x.CustomerTaxCode == normalizedTaxCode &&
                (fromDate == null || x.AsOfDate >= fromDate) &&
                (toDate == null || x.AsOfDate <= toDate))
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.CreatedAt)
            .Take(boundedTake)
            .ToListAsync(ct);

        return rows
            .OrderBy(x => x.AsOfDate)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new RiskScoreHistoryPoint(
                x.AsOfDate,
                x.Score,
                x.Signal,
                x.ModelVersion,
                x.CreatedAt))
            .ToList();
    }

    public async Task<RiskSnapshotCaptureResult> CaptureRiskSnapshotsAsync(
        DateOnly asOfDate,
        decimal absoluteThreshold,
        decimal relativeThresholdRatio,
        CancellationToken ct)
    {
        if (absoluteThreshold < 0m)
        {
            throw new InvalidOperationException("Absolute threshold must be non-negative.");
        }

        if (relativeThresholdRatio < 0m)
        {
            throw new InvalidOperationException("Relative threshold ratio must be non-negative.");
        }

        await using var connection = _connectionFactory.CreateRead();
        await connection.OpenAsync(ct);

        var rows = (await connection.QueryAsync<RiskSnapshotRow>(
            new CommandDefinition(RiskSnapshotListSql, new { asOf = asOfDate }, cancellationToken: ct)))
            .Where(x => !string.IsNullOrWhiteSpace(x.CustomerTaxCode))
            .ToList();

        if (rows.Count == 0)
        {
            return new RiskSnapshotCaptureResult(asOfDate, 0, 0, 0);
        }

        var activeModel = await _riskAiModelService.GetActiveModelAsync(modelKey: null, ct);
        var modelVersion = activeModel is null
            ? null
            : $"{activeModel.ModelKey}:{activeModel.Version}";

        var rowByCustomer = rows
            .GroupBy(x => x.CustomerTaxCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var existingForDate = await _db.RiskScoreSnapshots
            .Where(x => x.AsOfDate == asOfDate)
            .Select(x => x.CustomerTaxCode)
            .ToListAsync(ct);
        var existingCustomerSet = existingForDate.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTimeOffset.UtcNow;
        var newSnapshots = new List<RiskScoreSnapshot>(rowByCustomer.Count);
        foreach (var entry in rowByCustomer)
        {
            if (existingCustomerSet.Contains(entry.Key))
            {
                continue;
            }

            var row = entry.Value;
            var prediction = _riskAiModelService.Predict(new RiskMetrics(
                row.TotalOutstanding,
                row.OverdueAmount,
                row.OverdueRatio,
                row.MaxDaysPastDue,
                row.LateCount), asOfDate);

            var snapshot = new RiskScoreSnapshot
            {
                Id = Guid.NewGuid(),
                CustomerTaxCode = entry.Key,
                AsOfDate = asOfDate,
                Score = prediction.Probability,
                Signal = NormalizeSnapshotSignal(prediction.Signal),
                ModelVersion = modelVersion,
                CreatedAt = now
            };
            newSnapshots.Add(snapshot);
        }

        if (newSnapshots.Count == 0)
        {
            return new RiskSnapshotCaptureResult(asOfDate, 0, 0, 0);
        }

        _db.RiskScoreSnapshots.AddRange(newSnapshots);

        var customerCodes = newSnapshots
            .Select(x => x.CustomerTaxCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var previousRows = await _db.RiskScoreSnapshots.AsNoTracking()
            .Where(x => customerCodes.Contains(x.CustomerTaxCode) && x.AsOfDate < asOfDate)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var previousByCustomer = previousRows
            .GroupBy(x => x.CustomerTaxCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var existingAlerts = await _db.RiskDeltaAlerts
            .Where(x => x.AsOfDate == asOfDate && customerCodes.Contains(x.CustomerTaxCode))
            .Select(x => x.CustomerTaxCode)
            .ToListAsync(ct);
        var existingAlertSet = existingAlerts.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var alertCount = 0;
        var notificationCount = 0;
        foreach (var snapshot in newSnapshots)
        {
            if (!previousByCustomer.TryGetValue(snapshot.CustomerTaxCode, out var previous))
            {
                continue;
            }

            if (existingAlertSet.Contains(snapshot.CustomerTaxCode))
            {
                continue;
            }

            var delta = snapshot.Score - previous.Score;
            var absDelta = Math.Abs(delta);
            var relativeDelta = previous.Score <= 0m
                ? (snapshot.Score > 0m ? 1m : 0m)
                : absDelta / previous.Score;

            if (absDelta < absoluteThreshold && relativeDelta < relativeThresholdRatio)
            {
                continue;
            }

            var effectiveThreshold = Math.Max(
                absoluteThreshold,
                Math.Round(previous.Score * relativeThresholdRatio, 4, MidpointRounding.AwayFromZero));

            _db.RiskDeltaAlerts.Add(new RiskDeltaAlert
            {
                Id = Guid.NewGuid(),
                CustomerTaxCode = snapshot.CustomerTaxCode,
                AsOfDate = asOfDate,
                PrevScore = previous.Score,
                CurrScore = snapshot.Score,
                Delta = delta,
                Threshold = effectiveThreshold,
                Status = "OPEN",
                DetectedAt = now,
                ResolvedAt = null,
                CreatedAt = now,
                UpdatedAt = now
            });

            alertCount += 1;
            existingAlertSet.Add(snapshot.CustomerTaxCode);

            if (rowByCustomer.TryGetValue(snapshot.CustomerTaxCode, out var row) && row.OwnerId.HasValue)
            {
                _db.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = row.OwnerId.Value,
                    Title = $"Canh bao bien dong rui ro: {snapshot.CustomerTaxCode}",
                    Body =
                        $"Diem rui ro thay doi {delta:+0.0000;-0.0000;0.0000} (tu {previous.Score:0.0000} len {snapshot.Score:0.0000}).",
                    Severity = "WARN",
                    Source = "RISK_DELTA",
                    Metadata = JsonSerializer.Serialize(new
                    {
                        snapshot.CustomerTaxCode,
                        asOfDate,
                        previousScore = previous.Score,
                        currentScore = snapshot.Score,
                        delta,
                        absDelta,
                        relativeDelta,
                        threshold = effectiveThreshold,
                        row.CustomerName,
                        row.OwnerId,
                        row.OwnerName
                    }),
                    CreatedAt = now
                });
                notificationCount += 1;
            }
        }

        await _db.SaveChangesAsync(ct);

        return new RiskSnapshotCaptureResult(asOfDate, newSnapshots.Count, alertCount, notificationCount);
    }

    private static string? NormalizeAlertStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return status.Trim().ToUpperInvariant() switch
        {
            "OPEN" => "OPEN",
            "ACKED" => "ACKED",
            "RESOLVED" => "RESOLVED",
            _ => null
        };
    }

    private static string NormalizeSnapshotSignal(string? signal)
    {
        return signal?.Trim().ToUpperInvariant() switch
        {
            "CRITICAL" => "VERY_HIGH",
            "VERY_HIGH" => "VERY_HIGH",
            "HIGH" => "HIGH",
            "MEDIUM" => "MEDIUM",
            _ => "LOW"
        };
    }

    private sealed class RiskSnapshotRow
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
    }
}
