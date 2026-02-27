namespace CongNoGolden.Application.Imports;

public sealed record ImportCommitResult(
    int InsertedInvoices,
    int InsertedAdvances,
    int InsertedReceipts,
    int TotalEligibleRows = 0,
    int CommittedRows = 0,
    int SkippedRows = 0,
    IReadOnlyList<ImportCommitProgressStep>? ProgressSteps = null
);
