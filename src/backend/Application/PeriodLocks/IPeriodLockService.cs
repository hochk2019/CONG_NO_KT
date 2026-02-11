namespace CongNoGolden.Application.PeriodLocks;

public interface IPeriodLockService
{
    Task<PeriodLockDto> LockAsync(PeriodLockCreateRequest request, CancellationToken ct);
    Task<PeriodLockDto> UnlockAsync(Guid id, PeriodLockUnlockRequest request, CancellationToken ct);
    Task<IReadOnlyList<PeriodLockDto>> ListAsync(CancellationToken ct);
}
