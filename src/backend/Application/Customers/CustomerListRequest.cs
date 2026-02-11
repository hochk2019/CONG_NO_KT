namespace CongNoGolden.Application.Customers;

public sealed record CustomerListRequest(
    string? Search,
    Guid? OwnerId,
    string? Status,
    int Page,
    int PageSize);
