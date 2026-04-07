namespace CongNoGolden.Application.Customers;

public sealed record CustomerListRequest(
    string? Search,
    Guid? OwnerId,
    bool UnassignedOnly,
    string? Status,
    string? Sort,
    int Page,
    int PageSize);
