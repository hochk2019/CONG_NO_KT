namespace CongNoGolden.Application.Customers;

public sealed record CustomerReceiptDto(
    Guid Id,
    string? ReceiptNo,
    DateOnly ReceiptDate,
    DateOnly? AppliedPeriodStart,
    decimal Amount,
    decimal UnallocatedAmount,
    bool AutoAllocateEnabled,
    int Version,
    string Status,
    string SellerTaxCode,
    string? SellerShortName);
