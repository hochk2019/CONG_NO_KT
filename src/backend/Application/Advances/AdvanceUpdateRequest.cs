namespace CongNoGolden.Application.Advances;

public sealed record AdvanceUpdateRequest(
    string? AdvanceNo,
    DateOnly? AdvanceDate,
    decimal? Amount,
    string? Description,
    string? Reason,
    int? Version
);
