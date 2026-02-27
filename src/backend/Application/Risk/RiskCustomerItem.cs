namespace CongNoGolden.Application.Risk;

public sealed record RiskCustomerItem(
    string CustomerTaxCode,
    string CustomerName,
    Guid? OwnerId,
    string? OwnerName,
    decimal TotalOutstanding,
    decimal OverdueAmount,
    decimal OverdueRatio,
    int MaxDaysPastDue,
    int LateCount,
    string RiskLevel,
    decimal PredictedOverdueProbability,
    string AiSignal,
    IReadOnlyList<RiskAiFactorItem> AiFactors,
    string AiRecommendation);
