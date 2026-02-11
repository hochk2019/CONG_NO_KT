namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptOpenItemDto(
    string TargetType,
    Guid TargetId,
    string? DocumentNo,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal OutstandingAmount,
    string SellerTaxCode,
    string CustomerTaxCode);

public sealed record ReceiptReminderUpdateRequest(
    bool Disabled);
