namespace Ops.Shared.Models;

public sealed record BackupScheduleDto(bool Enabled, string TimeOfDay, int RetentionCount);
