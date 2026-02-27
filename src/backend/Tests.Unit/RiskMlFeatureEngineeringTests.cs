using CongNoGolden.Domain.Risk;
using CongNoGolden.Infrastructure.Services.RiskMl;
using Xunit;

namespace Tests.Unit;

public sealed class RiskMlFeatureEngineeringTests
{
    [Fact]
    public void BuildFeatureVector_ProducesExpectedSeasonalityEncoding()
    {
        var metrics = new RiskMetrics(
            TotalOutstanding: 100_000_000m,
            OverdueAmount: 25_000_000m,
            OverdueRatio: 0.25m,
            MaxDaysPastDue: 20,
            LateCount: 3);

        var january = RiskMlFeatureEngineering.BuildFeatureVector(metrics, new DateOnly(2026, 1, 31));
        var july = RiskMlFeatureEngineering.BuildFeatureVector(metrics, new DateOnly(2026, 7, 31));

        Assert.Equal(RiskMlFeatureEngineering.FeatureNames.Length, january.Length);
        Assert.Equal(RiskMlFeatureEngineering.FeatureNames.Length, july.Length);

        Assert.InRange(january[5], -0.0001, 0.0001);
        Assert.InRange(january[6], 0.9999, 1.0001);
        Assert.InRange(july[5], -0.0001, 0.0001);
        Assert.InRange(july[6], -1.0001, -0.9999);
    }
}

