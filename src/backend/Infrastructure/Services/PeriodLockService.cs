using System.Text.RegularExpressions;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.PeriodLocks;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed class PeriodLockService : IPeriodLockService
{
    private static readonly Regex QuarterRegex = new("^\\d{4}-Q[1-4]$", RegexOptions.Compiled);
    private static readonly Regex YearRegex = new("^\\d{4}$", RegexOptions.Compiled);

    private readonly ConGNoDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public PeriodLockService(ConGNoDbContext db, ICurrentUser currentUser, IAuditService auditService)
    {
        _db = db;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<PeriodLockDto> LockAsync(PeriodLockCreateRequest request, CancellationToken ct)
    {
        EnsureUser();

        var periodType = NormalizeType(request.PeriodType);
        var periodKey = NormalizeKey(periodType, request.PeriodKey);

        var existing = await _db.PeriodLocks
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PeriodType == periodType && p.PeriodKey == periodKey, ct);

        if (existing is not null)
        {
            return Map(existing);
        }

        var entity = new PeriodLock
        {
            Id = Guid.NewGuid(),
            PeriodType = periodType,
            PeriodKey = periodKey,
            LockedAt = DateTimeOffset.UtcNow,
            LockedBy = _currentUser.UserId,
            Note = request.Note
        };

        _db.PeriodLocks.Add(entity);
        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            "PERIOD_LOCK",
            "PeriodLock",
            entity.Id.ToString(),
            null,
            new { entity.PeriodType, entity.PeriodKey, entity.Note },
            ct);

        return Map(entity);
    }

    public async Task<PeriodLockDto> UnlockAsync(Guid id, PeriodLockUnlockRequest request, CancellationToken ct)
    {
        EnsureUser();

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Unlock reason is required.");
        }

        var entity = await _db.PeriodLocks.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null)
        {
            throw new InvalidOperationException("Period lock not found.");
        }

        var dto = Map(entity);

        _db.PeriodLocks.Remove(entity);
        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            "PERIOD_UNLOCK",
            "PeriodLock",
            entity.Id.ToString(),
            new { entity.PeriodType, entity.PeriodKey, entity.Note },
            new { reason = request.Reason },
            ct);

        return dto;
    }

    public async Task<IReadOnlyList<PeriodLockDto>> ListAsync(CancellationToken ct)
    {
        var locks = await _db.PeriodLocks
            .AsNoTracking()
            .OrderByDescending(p => p.LockedAt)
            .Select(p => new PeriodLockDto(p.Id, p.PeriodType, p.PeriodKey, p.LockedAt, p.LockedBy, p.Note))
            .ToListAsync(ct);

        return locks;
    }

    private void EnsureUser()
    {
        if (_currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("User context missing.");
        }
    }

    private static string NormalizeType(string? periodType)
    {
        var normalized = (periodType ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized is not ("MONTH" or "QUARTER" or "YEAR"))
        {
            throw new InvalidOperationException("Invalid period type.");
        }

        return normalized;
    }

    private static string NormalizeKey(string periodType, string? periodKey)
    {
        var value = (periodKey ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Period key is required.");
        }

        if (periodType == "MONTH")
        {
            if (DateOnly.TryParse(value, out var parsed))
            {
                return parsed.ToString("yyyy-MM");
            }

            if (DateOnly.TryParse(value + "-01", out parsed))
            {
                return parsed.ToString("yyyy-MM");
            }

            throw new InvalidOperationException("Invalid month key. Use YYYY-MM.");
        }

        if (periodType == "QUARTER")
        {
            if (!QuarterRegex.IsMatch(value))
            {
                throw new InvalidOperationException("Invalid quarter key. Use YYYY-QN.");
            }

            return value;
        }

        if (!YearRegex.IsMatch(value))
        {
            throw new InvalidOperationException("Invalid year key. Use YYYY.");
        }

        return value;
    }

    private static PeriodLockDto Map(PeriodLock entity)
    {
        return new PeriodLockDto(
            entity.Id,
            entity.PeriodType,
            entity.PeriodKey,
            entity.LockedAt,
            entity.LockedBy,
            entity.Note);
    }
}
