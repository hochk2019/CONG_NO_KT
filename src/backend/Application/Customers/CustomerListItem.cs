namespace CongNoGolden.Application.Customers;

public sealed record CustomerListItem(
    string TaxCode,
    string Name,
    string? OwnerName,
    decimal CurrentBalance,
    string Status);
