using System.Text.Json;
using CongNoGolden.Infrastructure.Services;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public class ImportCommitJsonTests
{
    [Theory]
    [InlineData("2025-12-31", 2025, 12, 31)]
    [InlineData("31/12/2025", 2025, 12, 31)]
    [InlineData("31-12-2025", 2025, 12, 31)]
    public void GetDate_Accepts_Multiple_Formats(string value, int year, int month, int day)
    {
        using var doc = JsonDocument.Parse($"{{\"receipt_date\":\"{value}\"}}");

        var result = ImportCommitJson.GetDate(doc.RootElement, "receipt_date");

        Assert.NotNull(result);
        Assert.Equal(new DateOnly(year, month, day), result!.Value);
    }
}
