namespace CongNoGolden.Application.Backups;

public static class BackupScheduleCalculator
{
    public static DateTimeOffset GetLastExpectedRunAt(
        DateTimeOffset now,
        int frequency,
        DayOfWeek targetDay,
        TimeSpan targetTime,
        TimeZoneInfo timezone)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, timezone);
        int daysSince;

        if (frequency == 1) // Daily
        {
            daysSince = 0;
            if (localNow.TimeOfDay < targetTime)
            {
                daysSince = 1;
            }
        }
        else // Weekly
        {
            daysSince = ((int)localNow.DayOfWeek - (int)targetDay + 7) % 7;
            if (daysSince == 0 && localNow.TimeOfDay < targetTime)
            {
                daysSince = 7;
            }
        }

        var targetLocalDate = localNow.Date.AddDays(-daysSince).Add(targetTime);

        var unspecifiedLocal = DateTime.SpecifyKind(targetLocalDate, DateTimeKind.Unspecified);
        var offset = timezone.GetUtcOffset(unspecifiedLocal);
        return new DateTimeOffset(targetLocalDate, offset);
    }
}
