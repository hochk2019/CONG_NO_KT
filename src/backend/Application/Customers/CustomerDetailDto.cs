namespace CongNoGolden.Application.Customers;

public sealed record CustomerDetailDto(
    string TaxCode,
    string Name,
    string? Address,
    string? Email,
    string? Phone,
    string Status,
    decimal CurrentBalance,
    int PaymentTermsDays,
    decimal? CreditLimit,
    Guid? OwnerId,
    string? OwnerName,
    Guid? ManagerId,
    string? ManagerName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
