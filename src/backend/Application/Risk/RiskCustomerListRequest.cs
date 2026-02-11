namespace CongNoGolden.Application.Risk;

public sealed record RiskCustomerListRequest(
    string? Search,
    Guid? OwnerId,
    string? Level,
    DateOnly? AsOfDate,
    int Page,
    int PageSize,
    string? Sort,
    string? Order);
