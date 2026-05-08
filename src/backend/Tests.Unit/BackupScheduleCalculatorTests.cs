using System.Reflection;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class BackupScheduleCalculatorTests
{
    [Fact]
    public void GetLastExpectedRunAt_WhenWeekly_ReturnsPastOrPresentTarget()
    {
        var type = Type.GetType("CongNoGolden.Application.Backups.BackupScheduleCalculator, CongNoGolden.Application");
        var method = type!.GetMethod("GetLastExpectedRunAt", BindingFlags.Public | BindingFlags.Static);

        var now = new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero); // Monday 10:00 UTC
        var targetTime = new TimeSpan(9, 0, 0);
        var timezone = TimeZoneInfo.Utc;

        // Passed target: 10:00 vs 09:00 -> expected is same day 9:00
        var result1 = (DateTimeOffset)method!.Invoke(null, new object[] { now, 2, DayOfWeek.Monday, targetTime, timezone })!;
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero), result1);

        // Target in future: 10:00 vs 11:00 -> expected is LAST week 11:00 (i.e. Dec 29)
        var targetTime2 = new TimeSpan(11, 0, 0);
        var result2 = (DateTimeOffset)method!.Invoke(null, new object[] { now, 2, DayOfWeek.Monday, targetTime2, timezone })!;
        Assert.Equal(new DateTimeOffset(2025, 12, 29, 11, 0, 0, TimeSpan.Zero), result2);

        // Different day (Target is Wed, now is Mon) -> expected is LAST Wed
        var result3 = (DateTimeOffset)method!.Invoke(null, new object[] { now, 2, DayOfWeek.Wednesday, targetTime, timezone })!;
        Assert.Equal(new DateTimeOffset(2025, 12, 31, 9, 0, 0, TimeSpan.Zero), result3);
    }

    [Fact]
    public void GetLastExpectedRunAt_WhenDaily_ReturnsPastOrPresentTarget()
    {
        var type = Type.GetType("CongNoGolden.Application.Backups.BackupScheduleCalculator, CongNoGolden.Application");
        var method = type!.GetMethod("GetLastExpectedRunAt", BindingFlags.Public | BindingFlags.Static);

        var now = new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero); // Monday 10:00 UTC
        var targetTime = new TimeSpan(11, 0, 0);
        var timezone = TimeZoneInfo.Utc;

        // If target > now, expected was yesterday
        var result1 = (DateTimeOffset)method!.Invoke(null, new object[] { now, 1, DayOfWeek.Monday, targetTime, timezone })!;
        Assert.Equal(new DateTimeOffset(2026, 1, 4, 11, 0, 0, TimeSpan.Zero), result1);

        // If target <= now, expected is today
        var targetTime2 = new TimeSpan(9, 0, 0);
        var result2 = (DateTimeOffset)method!.Invoke(null, new object[] { now, 1, DayOfWeek.Monday, targetTime2, timezone })!;
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero), result2);
    }
}
