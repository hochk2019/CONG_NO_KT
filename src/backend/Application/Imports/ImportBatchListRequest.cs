namespace CongNoGolden.Application.Imports;

public sealed record ImportBatchListRequest(
    string? Type,
    string? Status,
    string? Search,
    int Page,
    int PageSize);
