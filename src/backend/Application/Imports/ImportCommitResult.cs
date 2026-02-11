namespace CongNoGolden.Application.Imports;

public sealed record ImportCommitResult(
    int InsertedInvoices,
    int InsertedAdvances,
    int InsertedReceipts
);
