namespace Ops.Agent.Services;

public static class BackupScheduleCalculator
{
    public static bool TryParseTimeOfDay(string input, out TimeSpan timeOfDay)
    {
        timeOfDay = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        return TimeSpan.TryParseExact(input.Trim(), "hh\\:mm", null, out timeOfDay)
               || TimeSpan.TryParse(input.Trim(), out timeOfDay);
    }

    public static DateTimeOffset GetNextRun(DateTimeOffset now, TimeSpan timeOfDay)
    {
        var today = now.Date + timeOfDay;
        var candidate = new DateTimeOffset(today, now.Offset);
        return candidate <= now ? candidate.AddDays(1) : candidate;
    }
}
