namespace CongNoGolden.Application.Imports;

public sealed record ImportRollbackResult(
    int RolledBackInvoices,
    int RolledBackAdvances,
    int RolledBackReceipts
);
