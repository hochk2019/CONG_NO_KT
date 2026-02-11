using CongNoGolden.Domain.Risk;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public class RiskClassifierTests
{
    [Fact]
    public void Classify_Picks_Highest_Matching_Level()
    {
        var rules = new[]
        {
            new RiskRule(RiskLevel.VeryHigh, 90, 0.6m, 4),
            new RiskRule(RiskLevel.High, 60, 0.4m, 3),
            new RiskRule(RiskLevel.Medium, 30, 0.2m, 2),
            new RiskRule(RiskLevel.Low, 0, 0m, 0)
        };

        var veryHigh = RiskClassifier.Classify(
            new RiskMetrics(100, 70, 0.7m, 10, 1),
            rules);
        Assert.Equal(RiskLevel.VeryHigh, veryHigh);

        var high = RiskClassifier.Classify(
            new RiskMetrics(100, 40, 0.4m, 70, 1),
            rules);
        Assert.Equal(RiskLevel.High, high);

        var medium = RiskClassifier.Classify(
            new RiskMetrics(100, 10, 0.1m, 20, 2),
            rules);
        Assert.Equal(RiskLevel.Medium, medium);

        var low = RiskClassifier.Classify(
            new RiskMetrics(100, 0, 0m, 0, 0),
            rules);
        Assert.Equal(RiskLevel.Low, low);
    }
}
