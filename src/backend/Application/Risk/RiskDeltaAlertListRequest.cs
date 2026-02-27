namespace CongNoGolden.Application.Risk;

public sealed record RiskDeltaAlertListRequest(
    string? Status,
    string? CustomerTaxCode,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int Page,
    int PageSize);

public sealed record RiskDeltaAlertItem(
    Guid Id,
    string CustomerTaxCode,
    DateOnly AsOfDate,
    decimal PrevScore,
    decimal CurrScore,
    decimal Delta,
    decimal Threshold,
    string Status,
    DateTimeOffset DetectedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
