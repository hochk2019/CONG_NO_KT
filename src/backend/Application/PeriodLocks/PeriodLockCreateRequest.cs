namespace CongNoGolden.Application.PeriodLocks;

public sealed record PeriodLockCreateRequest(
    string PeriodType,
    string PeriodKey,
    string? Note
);
