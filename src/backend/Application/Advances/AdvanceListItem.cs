namespace CongNoGolden.Application.Advances;

public sealed record AdvanceListItem(
    Guid Id,
    string Status,
    int Version,
    string? AdvanceNo,
    DateOnly AdvanceDate,
    decimal Amount,
    decimal OutstandingAmount,
    string SellerTaxCode,
    string CustomerTaxCode,
    string? Description,
    string? CustomerName,
    string? OwnerName,
    string? SourceType,
    Guid? SourceBatchId,
    bool CanManage);
