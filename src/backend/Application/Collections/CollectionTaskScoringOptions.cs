namespace CongNoGolden.Application.Collections;

public sealed class CollectionTaskScoringOptions
{
    public decimal DefaultMinPriorityScore { get; set; } = 0.35m;

    public decimal OverdueAmountCap { get; set; } = 500_000_000m;
    public decimal ExposureAmountCap { get; set; } = 800_000_000m;

    public decimal ExpectedValueOverdueBlend { get; set; } = 0.70m;
    public decimal ExpectedValueExposureBlend { get; set; } = 0.30m;

    public decimal ExpectedValueWeight { get; set; } = 0.35m;
    public decimal ProbabilityWeight { get; set; } = 0.15m;
    public decimal OverdueRatioWeight { get; set; } = 0.15m;
    public decimal DaysPastDueWeight { get; set; } = 0.30m;
    public decimal RiskLevelWeight { get; set; } = 0.05m;

    public int DaysBand1End { get; set; } = 30;
    public int DaysBand2End { get; set; } = 60;
    public int DaysBand3End { get; set; } = 90;
    public int DaysBand4End { get; set; } = 180;

    public decimal DaysBand1Score { get; set; } = 0.30m;
    public decimal DaysBand2Score { get; set; } = 0.60m;
    public decimal DaysBand3Score { get; set; } = 0.80m;
    public decimal DaysBand4Score { get; set; } = 1.00m;

    public decimal RiskLevelVeryHighFactor { get; set; } = 1.00m;
    public decimal RiskLevelHighFactor { get; set; } = 0.85m;
    public decimal RiskLevelMediumFactor { get; set; } = 0.60m;
    public decimal RiskLevelLowFactor { get; set; } = 0.25m;
}
