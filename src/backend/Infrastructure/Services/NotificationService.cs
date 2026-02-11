using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Notifications;
using Dapper;
using Microsoft.EntityFrameworkCore;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using System.Text.Json;

namespace CongNoGolden.Infrastructure.Services;

public sealed class NotificationService : INotificationService
{
    private static readonly string[] DefaultPopupSeverities = ["WARN", "ALERT"];
    private static readonly string[] DefaultPopupSources = ["RISK", "RECEIPT", "IMPORT", "SYSTEM"];
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ConGNoDbContext _db;
    private readonly ICurrentUser _currentUser;

    public NotificationService(IDbConnectionFactory connectionFactory, ConGNoDbContext db, ICurrentUser currentUser)
    {
        _connectionFactory = connectionFactory;
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<NotificationItem>> ListAsync(NotificationListRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("User context missing.");
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 5 or > 100 ? 20 : request.PageSize;
        var offset = (page - 1) * pageSize;
        var unreadOnly = request.UnreadOnly ?? false;
        var source = NormalizeToken(request.Source);
        var severity = NormalizeToken(request.Severity);
        var query = string.IsNullOrWhiteSpace(request.Query) ? null : request.Query.Trim();

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(NotificationListSql, new
            {
                userId = _currentUser.UserId,
                unreadOnly,
                source,
                severity,
                query,
                limit = pageSize,
                offset
            }, cancellationToken: ct));

        var total = await multi.ReadSingleAsync<int>();
        var rows = (await multi.ReadAsync<NotificationRow>()).ToList();
        var items = rows.Select(r => new NotificationItem(
            r.Id,
            r.Title ?? string.Empty,
            r.Body,
            r.Severity ?? "INFO",
            r.Source ?? "RISK",
            r.CreatedAt,
            r.ReadAt)).ToList();

        return new PagedResult<NotificationItem>(items, page, pageSize, total);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("User context missing.");
        }

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == _currentUser.UserId, ct);
        if (notification is null)
        {
            return;
        }

        if (notification.ReadAt is not null)
        {
            return;
        }

        notification.ReadAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<NotificationUnreadCount> GetUnreadCountAsync(CancellationToken ct)
    {
        if (_currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("User context missing.");
        }

        var count = await _db.Notifications
            .AsNoTracking()
            .LongCountAsync(n => n.UserId == _currentUser.UserId && n.ReadAt == null, ct);

        return new NotificationUnreadCount((int)count);
    }

    public async Task MarkAllReadAsync(CancellationToken ct)
    {
        if (_currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("User context missing.");
        }

        var now = DateTimeOffset.UtcNow;
        await _db.Notifications
            .Where(n => n.UserId == _currentUser.UserId && n.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadAt, now), ct);
    }

    public async Task<NotificationPreferencesDto> GetPreferencesAsync(CancellationToken ct)
    {
        if (_currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("User context missing.");
        }

        var preference = await _db.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == _currentUser.UserId, ct);

        if (preference is null)
        {
            return new NotificationPreferencesDto(
                ReceiveNotifications: true,
                PopupEnabled: true,
                PopupSeverities: DefaultPopupSeverities,
                PopupSources: DefaultPopupSources);
        }

        return new NotificationPreferencesDto(
            preference.ReceiveNotifications,
            preference.PopupEnabled,
            DeserializeList(preference.PopupSeverities, DefaultPopupSeverities),
            DeserializeList(preference.PopupSources, DefaultPopupSources));
    }

    public async Task<NotificationPreferencesDto> UpdatePreferencesAsync(NotificationPreferencesUpdate request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("User context missing.");
        }

        var now = DateTimeOffset.UtcNow;
        var preference = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == _currentUser.UserId, ct);

        var severities = NormalizeList(request.PopupSeverities);
        var sources = NormalizeList(request.PopupSources);

        if (preference is null)
        {
            preference = new NotificationPreference
            {
                UserId = _currentUser.UserId.Value,
                ReceiveNotifications = request.ReceiveNotifications,
                PopupEnabled = request.PopupEnabled,
                PopupSeverities = JsonSerializer.Serialize(severities),
                PopupSources = JsonSerializer.Serialize(sources),
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.NotificationPreferences.Add(preference);
        }
        else
        {
            preference.ReceiveNotifications = request.ReceiveNotifications;
            preference.PopupEnabled = request.PopupEnabled;
            preference.PopupSeverities = JsonSerializer.Serialize(severities);
            preference.PopupSources = JsonSerializer.Serialize(sources);
            preference.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);

        return new NotificationPreferencesDto(
            preference.ReceiveNotifications,
            preference.PopupEnabled,
            severities,
            sources);
    }

    private const string NotificationListSql = @"
SELECT COUNT(*)
FROM congno.notifications
WHERE user_id = @userId
  AND (@unreadOnly = false OR read_at IS NULL)
  AND (@source IS NULL OR UPPER(source) = @source)
  AND (@severity IS NULL OR UPPER(severity) = @severity)
  AND (@query IS NULL OR title ILIKE '%' || @query || '%' OR body ILIKE '%' || @query || '%');

SELECT id AS id,
       title AS title,
       body AS body,
       severity AS severity,
       source AS source,
       created_at AS createdAt,
       read_at AS readAt
FROM congno.notifications
WHERE user_id = @userId
  AND (@unreadOnly = false OR read_at IS NULL)
  AND (@source IS NULL OR UPPER(source) = @source)
  AND (@severity IS NULL OR UPPER(severity) = @severity)
  AND (@query IS NULL OR title ILIKE '%' || @query || '%' OR body ILIKE '%' || @query || '%')
ORDER BY created_at DESC
LIMIT @limit OFFSET @offset;
";

    private static IReadOnlyList<string> DeserializeList(string? json, IReadOnlyList<string> fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            var list = JsonSerializer.Deserialize<string[]>(json);
            return list is { Length: > 0 } ? list : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string> values)
    {
        return values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim().ToUpperInvariant())
            .Distinct()
            .ToArray();
    }

    private static string? NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToUpperInvariant();
    }

    private sealed class NotificationRow
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Body { get; set; }
        public string? Severity { get; set; }
        public string? Source { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ReadAt { get; set; }
    }
}
