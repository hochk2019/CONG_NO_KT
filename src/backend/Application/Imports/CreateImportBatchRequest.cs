namespace CongNoGolden.Application.Imports;

public sealed record CreateImportBatchRequest(
    string Type,
    string Source,
    DateOnly? PeriodFrom,
    DateOnly? PeriodTo,
    string FileName,
    string FileHash,
    Guid? IdempotencyKey
);
