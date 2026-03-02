namespace CongNoGolden.Application.Backups;

public static class BackupScheduleCalculator
{
    public static DateTimeOffset GetNextRunAt(
        DateTimeOffset now,
        DayOfWeek targetDay,
        TimeSpan targetTime,
        TimeZoneInfo timezone)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, timezone);
        var daysUntil = ((int)targetDay - (int)localNow.DayOfWeek + 7) % 7;
        var targetLocalDate = localNow.Date.AddDays(daysUntil).Add(targetTime);

        if (daysUntil == 0 && localNow.TimeOfDay >= targetTime)
        {
            targetLocalDate = targetLocalDate.AddDays(7);
        }

        var unspecifiedLocal = DateTime.SpecifyKind(targetLocalDate, DateTimeKind.Unspecified);
        var offset = timezone.GetUtcOffset(unspecifiedLocal);
        return new DateTimeOffset(targetLocalDate, offset);
    }
}
