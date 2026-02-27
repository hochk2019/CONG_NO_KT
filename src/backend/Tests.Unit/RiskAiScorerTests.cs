using CongNoGolden.Domain.Risk;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class RiskAiScorerTests
{
    [Fact]
    public void Predict_ReturnsCriticalSignal_ForHighRiskMetrics()
    {
        var prediction = RiskAiScorer.Predict(new RiskMetrics(
            TotalOutstanding: 900_000_000m,
            OverdueAmount: 700_000_000m,
            OverdueRatio: 0.95m,
            MaxDaysPastDue: 140,
            LateCount: 12));

        Assert.Equal("CRITICAL", prediction.Signal);
        Assert.InRange(prediction.Probability, 0.80m, 1.00m);
        Assert.NotEmpty(prediction.Factors);
        Assert.False(string.IsNullOrWhiteSpace(prediction.Recommendation));
    }

    [Fact]
    public void Predict_ReturnsLowSignal_ForHealthyMetrics()
    {
        var prediction = RiskAiScorer.Predict(new RiskMetrics(
            TotalOutstanding: 25_000_000m,
            OverdueAmount: 0m,
            OverdueRatio: 0m,
            MaxDaysPastDue: 0,
            LateCount: 0));

        Assert.Equal("LOW", prediction.Signal);
        Assert.InRange(prediction.Probability, 0m, 0.35m);
        Assert.NotEmpty(prediction.Factors);
        Assert.False(string.IsNullOrWhiteSpace(prediction.Recommendation));
    }

    [Fact]
    public void Predict_IncreasesProbability_WhenRiskWorsens()
    {
        var baseline = RiskAiScorer.Predict(new RiskMetrics(
            TotalOutstanding: 100_000_000m,
            OverdueAmount: 20_000_000m,
            OverdueRatio: 0.2m,
            MaxDaysPastDue: 10,
            LateCount: 1));

        var worsened = RiskAiScorer.Predict(new RiskMetrics(
            TotalOutstanding: 200_000_000m,
            OverdueAmount: 160_000_000m,
            OverdueRatio: 0.8m,
            MaxDaysPastDue: 65,
            LateCount: 5));

        Assert.True(worsened.Probability > baseline.Probability);
    }

    [Fact]
    public void Predict_ClampsOutOfRangeInputs()
    {
        var prediction = RiskAiScorer.Predict(new RiskMetrics(
            TotalOutstanding: -10m,
            OverdueAmount: -5m,
            OverdueRatio: -4.7m,
            MaxDaysPastDue: -8,
            LateCount: -2));

        Assert.InRange(prediction.Probability, 0m, 1m);
        Assert.Equal("LOW", prediction.Signal);
        Assert.NotEmpty(prediction.Factors);
    }
}
