namespace CongNoGolden.Infrastructure.Data.Entities;

public sealed class Seller
{
    public string SellerTaxCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; }
}

public sealed class Customer
{
    public string TaxCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public Guid? AccountantOwnerId { get; set; }
    public Guid? ManagerUserId { get; set; }
    public decimal? CreditLimit { get; set; }
    public decimal CurrentBalance { get; set; }
    public int PaymentTermsDays { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; }
}
