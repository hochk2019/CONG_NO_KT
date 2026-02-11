using System.Reflection;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class BackupScheduleCalculatorTests
{
    [Fact]
    public void GetNextRunAt_WhenTargetLaterInWeek_ReturnsSameWeek()
    {
        var type = Type.GetType("CongNoGolden.Application.Backups.BackupScheduleCalculator, CongNoGolden.Application");
        Assert.NotNull(type);

        var method = type!.GetMethod("GetNextRunAt", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);

        var now = new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero); // Monday
        var targetDay = DayOfWeek.Wednesday;
        var targetTime = new TimeSpan(9, 0, 0);
        var timezone = TimeZoneInfo.Utc;

        var result = (DateTimeOffset)method!.Invoke(null, new object[] { now, targetDay, targetTime, timezone })!;

        Assert.Equal(new DateTimeOffset(2026, 1, 7, 9, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void GetNextRunAt_WhenTargetEarlierSameDay_ReturnsNextWeek()
    {
        var type = Type.GetType("CongNoGolden.Application.Backups.BackupScheduleCalculator, CongNoGolden.Application");
        Assert.NotNull(type);

        var method = type!.GetMethod("GetNextRunAt", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);

        var now = new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero); // Monday
        var targetDay = DayOfWeek.Monday;
        var targetTime = new TimeSpan(9, 0, 0);
        var timezone = TimeZoneInfo.Utc;

        var result = (DateTimeOffset)method!.Invoke(null, new object[] { now, targetDay, targetTime, timezone })!;

        Assert.Equal(new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero), result);
    }
}
