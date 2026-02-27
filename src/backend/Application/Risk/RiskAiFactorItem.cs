namespace CongNoGolden.Application.Risk;

public sealed record RiskAiFactorItem(
    string Code,
    string Label,
    decimal RawValue,
    decimal NormalizedValue,
    decimal Weight,
    decimal Contribution);
