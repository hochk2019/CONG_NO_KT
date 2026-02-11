namespace CongNoGolden.Application.PeriodLocks;

public sealed record PeriodLockDto(
    Guid Id,
    string PeriodType,
    string PeriodKey,
    DateTimeOffset LockedAt,
    Guid? LockedBy,
    string? Note
);
