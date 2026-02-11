namespace CongNoGolden.Application.Imports;

public sealed record ImportBatchDto(
    Guid BatchId,
    string Status,
    string? FileHash
);
