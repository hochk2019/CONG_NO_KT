namespace CongNoGolden.Infrastructure.Data.Entities;

public sealed class PeriodLock
{
    public Guid Id { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public string PeriodKey { get; set; } = string.Empty;
    public DateTimeOffset LockedAt { get; set; }
    public Guid? LockedBy { get; set; }
    public string? Note { get; set; }
}
