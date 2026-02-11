using Ops.Agent.Services;

namespace Ops.Tests;

public class BackupScheduleCalculatorTests
{
    [Fact]
    public void TryParseTimeOfDay_ParsesValidTime()
    {
        var ok = BackupScheduleCalculator.TryParseTimeOfDay("23:00", out var time);
        Assert.True(ok);
        Assert.Equal(new TimeSpan(23, 0, 0), time);
    }

    [Fact]
    public void GetNextRun_ReturnsSameDayIfFuture()
    {
        var now = new DateTimeOffset(2026, 2, 8, 22, 0, 0, TimeSpan.FromHours(7));
        var next = BackupScheduleCalculator.GetNextRun(now, new TimeSpan(23, 0, 0));
        Assert.Equal(new DateTimeOffset(2026, 2, 8, 23, 0, 0, TimeSpan.FromHours(7)), next);
    }

    [Fact]
    public void GetNextRun_ReturnsNextDayIfPast()
    {
        var now = new DateTimeOffset(2026, 2, 8, 23, 30, 0, TimeSpan.FromHours(7));
        var next = BackupScheduleCalculator.GetNextRun(now, new TimeSpan(23, 0, 0));
        Assert.Equal(new DateTimeOffset(2026, 2, 9, 23, 0, 0, TimeSpan.FromHours(7)), next);
    }
}
