namespace CongNoGolden.Application.Customers;

public sealed record CustomerListRequest(
    string? Search,
    Guid? OwnerId,
    string? Status,
    string? Sort,
    int Page,
    int PageSize);
