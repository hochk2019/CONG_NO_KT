namespace CongNoGolden.Application.Imports;

public sealed record ImportCommitProgressStep(
    string Stage,
    int Percent,
    int ProcessedRows,
    int TotalRows,
    string Message
);
