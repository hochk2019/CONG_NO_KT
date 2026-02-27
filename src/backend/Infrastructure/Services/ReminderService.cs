using System.Text.Json;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Reminders;
using CongNoGolden.Domain.Risk;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services.Common;
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
        _currentUser.EnsureUser();

        if (request.FrequencyDays < 1 || request.FrequencyDays > 30)
        {
            throw new InvalidOperationException("Frequency days must be between 1 and 30.");
        }

        if (request.UpcomingDueDays < 1 || request.UpcomingDueDays > 30)
        {
            throw new InvalidOperationException("Upcoming due days must be between 1 and 30.");
        }

        if (request.EscalationMaxAttempts < 1 || request.EscalationMaxAttempts > 20)
        {
            throw new InvalidOperationException("Escalation max attempts must be between 1 and 20.");
        }

        if (request.EscalationCooldownHours < 0 || request.EscalationCooldownHours > 720)
        {
            throw new InvalidOperationException("Escalation cooldown hours must be between 0 and 720.");
        }

        if (request.EscalateToSupervisorAfter < 1 ||
            request.EscalateToSupervisorAfter > request.EscalationMaxAttempts)
        {
            throw new InvalidOperationException("Escalate-to-supervisor threshold is invalid.");
        }

        if (request.EscalateToAdminAfter < request.EscalateToSupervisorAfter ||
            request.EscalateToAdminAfter > request.EscalationMaxAttempts)
        {
            throw new InvalidOperationException("Escalate-to-admin threshold is invalid.");
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
            settings.EscalationMaxAttempts,
            settings.EscalationCooldownHours,
            settings.EscalateToSupervisorAfter,
            settings.EscalateToAdminAfter,
            settings.Channels,
            settings.TargetLevels,
            settings.LastRunAt,
            settings.NextRunAt
        };

        settings.Enabled = request.Enabled;
        settings.FrequencyDays = request.FrequencyDays;
        settings.UpcomingDueDays = request.UpcomingDueDays;
        settings.EscalationMaxAttempts = request.EscalationMaxAttempts;
        settings.EscalationCooldownHours = request.EscalationCooldownHours;
        settings.EscalateToSupervisorAfter = request.EscalateToSupervisorAfter;
        settings.EscalateToAdminAfter = request.EscalateToAdminAfter;
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
            settings.EscalationMaxAttempts,
            settings.EscalationCooldownHours,
            settings.EscalateToSupervisorAfter,
            settings.EscalateToAdminAfter,
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

    public async Task<PagedResult<ReminderLogItem>> ListLogsAsync(ReminderLogRequest request, CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var ownerId = _currentUser.ResolveOwnerFilter(request.OwnerId);
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
            EscalationMaxAttempts = 3,
            EscalationCooldownHours = 24,
            EscalateToSupervisorAfter = 2,
            EscalateToAdminAfter = 3,
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

    private static ReminderSettingsDto MapSettings(ReminderSetting settings)
    {
        return new ReminderSettingsDto(
            settings.Enabled,
            settings.FrequencyDays,
            settings.UpcomingDueDays,
            settings.EscalationMaxAttempts,
            settings.EscalationCooldownHours,
            settings.EscalateToSupervisorAfter,
            settings.EscalateToAdminAfter,
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
}
