namespace CongNoGolden.Application.Customers;

public sealed record CustomerBalanceDriftItem(
    string TaxCode,
    decimal CurrentBalance,
    decimal ExpectedBalance,
    decimal AbsoluteDrift);

public sealed record CustomerBalanceReconcileRequest(
    bool ApplyChanges = false,
    int MaxItems = 20,
    decimal Tolerance = 0.01m);

public sealed record CustomerBalanceReconcileResult(
    DateTimeOffset ExecutedAtUtc,
    int CheckedCustomers,
    int DriftedCustomers,
    int UpdatedCustomers,
    decimal TotalAbsoluteDrift,
    decimal MaxAbsoluteDrift,
    IReadOnlyList<CustomerBalanceDriftItem> TopDrifts);
