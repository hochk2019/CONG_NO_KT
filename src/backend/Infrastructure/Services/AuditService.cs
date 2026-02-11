using System.Text.Json;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;

namespace CongNoGolden.Infrastructure.Services;

public sealed class AuditService : IAuditService
{
    private readonly ConGNoDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AuditService(ConGNoDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task LogAsync(string action, string entityType, string entityId, object? before, object? after, CancellationToken ct)
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            BeforeData = before is null ? null : JsonSerializer.Serialize(before),
            AfterData = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = _currentUser.IpAddress,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }
}
