using System.Text.Json;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Reminders;
using CongNoGolden.Domain.Risk;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReminderService : IReminderService
{
    private static readonly string[] DefaultChannels = { "IN_APP", "ZALO" };
    private static readonly string[] DefaultLevels = { "VERY_HIGH", "HIGH", "MEDIUM" };

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ConGNoDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly IZaloClient _zaloClient;

    public ReminderService(
        IDbConnectionFactory connectionFactory,
        ConGNoDbContext db,
        ICurrentUser currentUser,
        IAuditService auditService,
        IZaloClient zaloClient)
    {
        _connectionFactory = connectionFactory;
        _db = db;
        _currentUser = currentUser;
        _auditService = auditService;
        _zaloClient = zaloClient;
    }

    public async Task<ReminderSettingsDto> GetSettingsAsync(CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(ct);
        return MapSettings(settings);
    }

    public async Task UpdateSettingsAsync(ReminderSettingsUpdateRequest request, CancellationToken ct)
    {
        EnsureUser();

        if (request.FrequencyDays < 1 || request.FrequencyDays > 30)
        {
            throw new InvalidOperationException("Frequency days must be between 1 and 30.");
        }

        if (request.UpcomingDueDays < 1 || request.UpcomingDueDays > 30)
        {
            throw new InvalidOperationException("Upcoming due days must be between 1 and 30.");
        }

        var channels = NormalizeStringList(request.Channels);
        if (channels.Count == 0)
        {
            throw new InvalidOperationException("At least one channel is required.");
        }

        var levels = NormalizeLevels(request.TargetLevels);
        if (levels.Count == 0)
        {
            throw new InvalidOperationException("At least one risk level is required.");
        }

        var settings = await GetOrCreateSettingsAsync(ct);
        var before = new
        {
            settings.Enabled,
            settings.FrequencyDays,
            settings.UpcomingDueDays,
            settings.Channels,
            settings.TargetLevels,
            settings.LastRunAt,
            settings.NextRunAt
        };

        settings.Enabled = request.Enabled;
        settings.FrequencyDays = request.FrequencyDays;
        settings.UpcomingDueDays = request.UpcomingDueDays;
        settings.Channels = JsonSerializer.Serialize(channels);
        settings.TargetLevels = JsonSerializer.Serialize(levels);
        settings.NextRunAt = ResolveNextRun(settings.LastRunAt, request.FrequencyDays);
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        var after = new
        {
            settings.Enabled,
            settings.FrequencyDays,
            settings.UpcomingDueDays,
            Channels = settings.Channels,
            TargetLevels = settings.TargetLevels,
            settings.LastRunAt,
            settings.NextRunAt
        };

        await _auditService.LogAsync(
            "REMINDER_SETTINGS_UPDATE",
            "ReminderSettings",
            settings.Id.ToString(),
            before,
            after,
            ct);
    }

    public async Task<ReminderRunResult> RunAsync(bool force, CancellationToken ct)
    {
        if (force)
        {
            EnsureUser();
        }

        var settings = await GetOrCreateSettingsAsync(ct);
        var now = DateTimeOffset.UtcNow;

        if (!force)
        {
            if (!settings.Enabled)
            {
                return new ReminderRunResult(now, 0, 0, 0, 0);
            }

            if (settings.NextRunAt.HasValue && settings.NextRunAt.Value > now)
            {
                return new ReminderRunResult(now, 0, 0, 0, 0);
            }
        }

        var channels = NormalizeStringList(ParseJsonList(settings.Channels, DefaultChannels));
        var levels = NormalizeLevels(ParseJsonList(settings.TargetLevels, DefaultLevels));
        if (channels.Count == 0 || levels.Count == 0)
        {
            return new ReminderRunResult(now, 0, 0, 0, 0);
        }

        var asOf = DateOnly.FromDateTime(now.Date);
        var candidates = await LoadReminderCandidatesAsync(asOf, levels, ct);

        var sent = 0;
        var failed = 0;
        var skipped = 0;
        var totalCandidates = candidates.Count;

        foreach (var candidate in candidates)
        {
            foreach (var channel in channels)
            {
                var outcome = await SendReminderAsync(candidate, channel, ct);
                switch (outcome.Status)
                {
                    case "SENT":
                        sent++;
                        break;
                    case "FAILED":
                        failed++;
                        break;
                    default:
                        skipped++;
                        break;
                }
            }
        }

        var upcomingOutcome = await SendUpcomingDueAsync(asOf, settings.UpcomingDueDays, ct);
        totalCandidates += upcomingOutcome.Candidates;
        sent += upcomingOutcome.Sent;
        failed += upcomingOutcome.Failed;
        skipped += upcomingOutcome.Skipped;

        settings.LastRunAt = now;
        settings.NextRunAt = ResolveNextRun(now, settings.FrequencyDays);
        settings.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        return new ReminderRunResult(now, totalCandidates, sent, failed, skipped);
    }

    public async Task<PagedResult<ReminderLogItem>> ListLogsAsync(ReminderLogRequest request, CancellationToken ct)
    {
        EnsureUser();

        var ownerId = ResolveOwnerFilter(request.OwnerId);
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 5 or > 200 ? 20 : request.PageSize;
        var offset = (page - 1) * pageSize;

        var channel = NormalizeChannel(request.Channel);
        var status = NormalizeStatus(request.Status);

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(ReminderLogListSql, new
            {
                ownerId,
                channel,
                status,
                limit = pageSize,
                offset
            }, cancellationToken: ct));

        var total = await multi.ReadSingleAsync<int>();
        var rows = (await multi.ReadAsync<ReminderLogRow>()).ToList();

        var items = rows.Select(r => new ReminderLogItem(
            r.Id,
            r.CustomerTaxCode ?? string.Empty,
            r.CustomerName ?? r.CustomerTaxCode ?? string.Empty,
            r.OwnerUserId,
            r.OwnerName,
            r.RiskLevel ?? "LOW",
            r.Channel ?? "IN_APP",
            r.Status ?? "SKIPPED",
            r.Message,
            r.ErrorDetail,
            r.SentAt,
            r.CreatedAt)).ToList();

        return new PagedResult<ReminderLogItem>(items, page, pageSize, total);
    }

    private async Task<ReminderSetting> GetOrCreateSettingsAsync(CancellationToken ct)
    {
        var settings = await _db.ReminderSettings.FirstOrDefaultAsync(ct);
        if (settings is not null)
        {
            return settings;
        }

        settings = new ReminderSetting
        {
            Id = Guid.NewGuid(),
            Singleton = true,
            Enabled = true,
            FrequencyDays = 7,
            UpcomingDueDays = 7,
            Channels = JsonSerializer.Serialize(DefaultChannels),
            TargetLevels = JsonSerializer.Serialize(DefaultLevels),
            NextRunAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.ReminderSettings.Add(settings);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    private async Task<IReadOnlyList<ReminderCandidateRow>> LoadReminderCandidatesAsync(
        DateOnly asOf,
        IReadOnlyList<string> levels,
        CancellationToken ct)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        var rows = await connection.QueryAsync<ReminderCandidateRow>(
            new CommandDefinition(ReminderCandidatesSql, new { asOf, levels = levels.ToArray() }, cancellationToken: ct));

        return rows.ToList();
    }

    private async Task<IReadOnlyList<UpcomingDueRow>> LoadUpcomingDueCandidatesAsync(
        DateOnly asOf,
        DateOnly toDate,
        CancellationToken ct)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        var rows = await connection.QueryAsync<UpcomingDueRow>(
            new CommandDefinition(UpcomingDueCandidatesSql, new { asOf, toDate }, cancellationToken: ct));

        return rows.ToList();
    }

    private async Task<ReminderOutcome> SendReminderAsync(ReminderCandidateRow candidate, string channel, CancellationToken ct)
    {
        if (candidate.OwnerId is null)
        {
            return LogOutcome(candidate, channel, "SKIPPED", "OWNER_MISSING", null);
        }

        var message = BuildMessage(candidate);
        if (channel == "IN_APP")
        {
            var preference = await _db.NotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == candidate.OwnerId.Value, ct);
            if (preference is not null && !preference.ReceiveNotifications)
            {
                return LogOutcome(candidate, channel, "SKIPPED", "PREF_DISABLED", null);
            }

            _db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = candidate.OwnerId.Value,
                Title = "Nhắc rủi ro công nợ",
                Body = message,
                Severity = "WARN",
                Source = "RISK",
                Metadata = JsonSerializer.Serialize(new
                {
                    candidate.CustomerTaxCode,
                    candidate.CustomerName,
                    candidate.RiskLevel,
                    candidate.OverdueAmount,
                    candidate.MaxDaysPastDue
                }),
                CreatedAt = DateTimeOffset.UtcNow
            });

            return LogOutcome(candidate, channel, "SENT", null, message);
        }

        if (channel == "ZALO")
        {
            var result = await _zaloClient.SendAsync(candidate.OwnerZaloUserId ?? string.Empty, message, ct);
            if (result.Success)
            {
                return LogOutcome(candidate, channel, "SENT", null, message);
            }

            var status = result.Error == "NOT_CONFIGURED" || result.Error == "MISSING_USER_ID" ? "SKIPPED" : "FAILED";
            return LogOutcome(candidate, channel, status, result.Error, message);
        }

        return LogOutcome(candidate, channel, "SKIPPED", "UNKNOWN_CHANNEL", null);
    }

    private async Task<UpcomingReminderOutcome> SendUpcomingDueAsync(DateOnly asOf, int upcomingDays, CancellationToken ct)
    {
        if (upcomingDays <= 0)
        {
            return new UpcomingReminderOutcome(0, 0, 0, 0);
        }

        var toDate = asOf.AddDays(upcomingDays);
        var rows = await LoadUpcomingDueCandidatesAsync(asOf, toDate, ct);
        if (rows.Count == 0)
        {
            return new UpcomingReminderOutcome(0, 0, 0, 0);
        }

        var supervisorIds = await LoadSupervisorIdsAsync(ct);

        var sent = 0;
        var failed = 0;
        var skipped = 0;

        foreach (var row in rows)
        {
            var recipients = new HashSet<Guid>();
            if (row.OwnerId.HasValue)
            {
                recipients.Add(row.OwnerId.Value);
            }

            foreach (var supervisorId in supervisorIds)
            {
                recipients.Add(supervisorId);
            }

            if (recipients.Count == 0)
            {
                skipped++;
                LogUpcomingOutcome(row, "IN_APP", "SKIPPED", "NO_RECIPIENT", null);
                continue;
            }

            var allowed = await FilterRecipientsAsync(recipients, ct);
            if (allowed.Count == 0)
            {
                skipped++;
                LogUpcomingOutcome(row, "IN_APP", "SKIPPED", "PREF_DISABLED", null);
                continue;
            }

            var message = BuildUpcomingDueMessage(row, upcomingDays);
            var metadata = JsonSerializer.Serialize(new
            {
                row.CustomerTaxCode,
                row.CustomerName,
                row.DueAmount,
                row.NearestDueDate,
                row.DocumentCount
            });

            foreach (var userId in allowed)
            {
                _db.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = "Sắp đến hạn thanh toán",
                    Body = message,
                    Severity = "WARN",
                    Source = "RISK",
                    Metadata = metadata,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            LogUpcomingOutcome(row, "IN_APP", "SENT", null, message);
            sent++;
        }

        return new UpcomingReminderOutcome(rows.Count, sent, failed, skipped);
    }

    private ReminderOutcome LogOutcome(
        ReminderCandidateRow candidate,
        string channel,
        string status,
        string? error,
        string? message)
    {
        var log = new ReminderLog
        {
            Id = Guid.NewGuid(),
            CustomerTaxCode = candidate.CustomerTaxCode ?? string.Empty,
            OwnerUserId = candidate.OwnerId,
            RiskLevel = candidate.RiskLevel ?? "LOW",
            Channel = channel,
            Status = status,
            Message = message,
            ErrorDetail = error,
            SentAt = status == "SENT" ? DateTimeOffset.UtcNow : null,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.ReminderLogs.Add(log);

        return new ReminderOutcome(status);
    }

    private void LogUpcomingOutcome(
        UpcomingDueRow row,
        string channel,
        string status,
        string? error,
        string? message)
    {
        var log = new ReminderLog
        {
            Id = Guid.NewGuid(),
            CustomerTaxCode = row.CustomerTaxCode ?? string.Empty,
            OwnerUserId = row.OwnerId,
            RiskLevel = "LOW",
            Channel = channel,
            Status = status,
            Message = message,
            ErrorDetail = error,
            SentAt = status == "SENT" ? DateTimeOffset.UtcNow : null,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.ReminderLogs.Add(log);
    }

    private static string BuildMessage(ReminderCandidateRow candidate)
    {
        var ratio = Math.Round(candidate.OverdueRatio * 100, 1);
        var riskLabel = ResolveRiskLabel(candidate.RiskLevel);
        var overdueAmount = FormatMoney(candidate.OverdueAmount);
        var totalOutstanding = FormatMoney(candidate.TotalOutstanding);

        return string.Join(Environment.NewLine, new[]
        {
            "Cảnh báo công nợ quá hạn",
            $"Khách hàng: {candidate.CustomerName} ({candidate.CustomerTaxCode})",
            $"Nhóm rủi ro: {riskLabel} • Quá hạn tối đa: {candidate.MaxDaysPastDue} ngày",
            $"Tỷ lệ quá hạn: {ratio}% • Giá trị quá hạn: {overdueAmount}",
            $"Tổng dư nợ: {totalOutstanding} • Số lần trễ: {candidate.LateCount}"
        });
    }

    private static string BuildUpcomingDueMessage(UpcomingDueRow row, int upcomingDays)
    {
        var dueAmount = FormatMoney(row.DueAmount);
        var dueDate = row.NearestDueDate.HasValue
            ? row.NearestDueDate.Value.ToString("dd/MM/yyyy")
            : "-";

        return string.Join(Environment.NewLine, new[]
        {
            "Nhắc công nợ sắp đến hạn",
            $"Khách hàng: {row.CustomerName} ({row.CustomerTaxCode})",
            $"Tổng đến hạn trong {upcomingDays} ngày: {dueAmount}",
            $"Hạn gần nhất: {dueDate} • Số chứng từ: {row.DocumentCount}"
        });
    }

    private static string ResolveRiskLabel(string? riskLevel)
    {
        return riskLevel?.Trim().ToUpperInvariant() switch
        {
            "VERY_HIGH" => "Rất cao",
            "HIGH" => "Cao",
            "MEDIUM" => "Trung bình",
            _ => "Thấp"
        };
    }

    private static string FormatMoney(decimal value)
    {
        return string.Format(System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), "{0:N0} đ", value);
    }

    private static ReminderSettingsDto MapSettings(ReminderSetting settings)
    {
        return new ReminderSettingsDto(
            settings.Enabled,
            settings.FrequencyDays,
            settings.UpcomingDueDays,
            NormalizeStringList(ParseJsonList(settings.Channels, DefaultChannels)),
            NormalizeLevels(ParseJsonList(settings.TargetLevels, DefaultLevels)),
            settings.LastRunAt,
            settings.NextRunAt);
    }

    private static IReadOnlyList<string> ParseJsonList(string? json, IReadOnlyList<string> fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback.ToArray();
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list is null || list.Count == 0 ? fallback.ToArray() : list;
        }
        catch
        {
            return fallback.ToArray();
        }
    }

    private static IReadOnlyList<string> NormalizeStringList(IEnumerable<string> values)
    {
        return values
            .Select(v => v.Trim().ToUpperInvariant())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct()
            .ToList();
    }

    private static IReadOnlyList<string> NormalizeLevels(IEnumerable<string> values)
    {
        var results = new List<string>();
        foreach (var value in values)
        {
            if (RiskLevelExtensions.TryParse(value, out var level))
            {
                results.Add(level.ToCode());
            }
        }
        return results.Distinct().ToList();
    }

    private static DateTimeOffset ResolveNextRun(DateTimeOffset? lastRun, int frequencyDays)
    {
        var anchor = lastRun ?? DateTimeOffset.UtcNow;
        return anchor.AddDays(frequencyDays);
    }

    private static string? NormalizeChannel(string? channel)
    {
        return string.IsNullOrWhiteSpace(channel) ? null : channel.Trim().ToUpperInvariant();
    }

    private static string? NormalizeStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
    }

    private async Task<IReadOnlyList<Guid>> LoadSupervisorIdsAsync(CancellationToken ct)
    {
        var supervisors = await _db.UserRoles
            .Join(_db.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Code })
            .Where(r => r.Code == "Supervisor")
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync(ct);

        return supervisors;
    }

    private async Task<IReadOnlyList<Guid>> FilterRecipientsAsync(
        IReadOnlySet<Guid> recipients,
        CancellationToken ct)
    {
        if (recipients.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var ids = recipients.ToArray();
        var disabled = await _db.NotificationPreferences
            .AsNoTracking()
            .Where(p => ids.Contains(p.UserId) && !p.ReceiveNotifications)
            .Select(p => p.UserId)
            .ToListAsync(ct);

        return ids.Where(id => !disabled.Contains(id)).ToList();
    }

    private Guid? ResolveOwnerFilter(Guid? explicitOwner)
    {
        var roles = new HashSet<string>(_currentUser.Roles, StringComparer.OrdinalIgnoreCase);
        var canViewAll = roles.Contains("Admin") || roles.Contains("Supervisor") || roles.Contains("Viewer");
        if (canViewAll)
        {
            return explicitOwner;
        }

        return _currentUser.UserId;
    }

    private void EnsureUser()
    {
        if (_currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("User context missing.");
        }
    }

    private sealed record ReminderOutcome(string Status);

    private sealed record UpcomingReminderOutcome(int Candidates, int Sent, int Failed, int Skipped);

    private sealed class ReminderCandidateRow
    {
        public string? CustomerTaxCode { get; set; }
        public string? CustomerName { get; set; }
        public Guid? OwnerId { get; set; }
        public string? OwnerName { get; set; }
        public string? OwnerZaloUserId { get; set; }
        public string? RiskLevel { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal OverdueAmount { get; set; }
        public decimal OverdueRatio { get; set; }
        public int MaxDaysPastDue { get; set; }
        public int LateCount { get; set; }
    }

    private sealed class ReminderLogRow
    {
        public Guid Id { get; set; }
        public string? CustomerTaxCode { get; set; }
        public string? CustomerName { get; set; }
        public Guid? OwnerUserId { get; set; }
        public string? OwnerName { get; set; }
        public string? RiskLevel { get; set; }
        public string? Channel { get; set; }
        public string? Status { get; set; }
        public string? Message { get; set; }
        public string? ErrorDetail { get; set; }
        public DateTimeOffset? SentAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    private sealed class UpcomingDueRow
    {
        public string? CustomerTaxCode { get; set; }
        public string? CustomerName { get; set; }
        public Guid? OwnerId { get; set; }
        public string? OwnerName { get; set; }
        public string? OwnerZaloUserId { get; set; }
        public decimal DueAmount { get; set; }
        public DateOnly? NearestDueDate { get; set; }
        public int DocumentCount { get; set; }
    }
}
