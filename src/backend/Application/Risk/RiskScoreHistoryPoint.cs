namespace CongNoGolden.Application.Risk;

public sealed record RiskScoreHistoryPoint(
    DateOnly AsOfDate,
    decimal Score,
    string Signal,
    string? ModelVersion,
    DateTimeOffset CreatedAt);
