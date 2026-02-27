using CongNoGolden.Domain.Risk;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public class RiskClassifierTests
{
    [Fact]
    public void Classify_Picks_Highest_Matching_Level_WithAnyMatchMode()
    {
        var rules = new[]
        {
            new RiskRule(RiskLevel.VeryHigh, 90, 0.6m, 4, MatchMode: RiskMatchMode.Any),
            new RiskRule(RiskLevel.High, 60, 0.4m, 3, MatchMode: RiskMatchMode.Any),
            new RiskRule(RiskLevel.Medium, 30, 0.2m, 2, MatchMode: RiskMatchMode.Any),
            new RiskRule(RiskLevel.Low, 0, 0m, 0, MatchMode: RiskMatchMode.Any)
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

    [Fact]
    public void Classify_WithAllMatchMode_RequiresAllThresholds()
    {
        var rules = new[]
        {
            new RiskRule(RiskLevel.High, 30, 0.3m, 2, MatchMode: RiskMatchMode.All),
            new RiskRule(RiskLevel.Low, 0, 0m, 0, MatchMode: RiskMatchMode.Any)
        };

        var onlyOneMetric = RiskClassifier.Classify(
            new RiskMetrics(100, 40, 0.4m, 5, 0),
            rules);
        Assert.Equal(RiskLevel.Low, onlyOneMetric);

        var allMetrics = RiskClassifier.Classify(
            new RiskMetrics(100, 40, 0.4m, 35, 2),
            rules);
        Assert.Equal(RiskLevel.High, allMetrics);
    }
}
