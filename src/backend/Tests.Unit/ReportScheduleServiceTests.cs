using CongNoGolden.Infrastructure.Services;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class ReportScheduleServiceTests
{
    [Fact]
    public void ParseCronExpression_AcceptsStandardAndSecondsFormats()
    {
        var standard = ReportScheduleService.ParseCronExpression("*/5 * * * *");
        var withSeconds = ReportScheduleService.ParseCronExpression("0 */10 * * * *");

        Assert.NotNull(standard.GetNextOccurrence(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            TimeZoneInfo.Utc));
        Assert.NotNull(withSeconds.GetNextOccurrence(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            TimeZoneInfo.Utc));
    }

    [Fact]
    public void ParseCronExpression_WithInvalidExpression_ThrowsInvalidOperation()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ReportScheduleService.ParseCronExpression("not-a-cron"));

        Assert.Contains("Biểu thức cron không hợp lệ", ex.Message);
    }

    [Fact]
    public void ResolveTimezone_WithInvalidTimezone_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ReportScheduleService.ResolveTimezone("Mars/Phobos"));
    }

    [Fact]
    public void CalculateNextRunUtc_ReturnsExpectedUtcTime()
    {
        var cron = ReportScheduleService.ParseCronExpression("* * * * *");
        var after = new DateTimeOffset(2026, 1, 1, 0, 0, 30, TimeSpan.Zero);

        var nextRun = ReportScheduleService.CalculateNextRunUtc(cron, TimeZoneInfo.Utc, after);

        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 1, 0, TimeSpan.Zero), nextRun);
    }
}
