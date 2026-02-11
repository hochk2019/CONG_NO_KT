namespace CongNoGolden.Infrastructure.Data.Entities;

public sealed class Invoice
{
    public Guid Id { get; set; }
    public string SellerTaxCode { get; set; } = string.Empty;
    public string CustomerTaxCode { get; set; } = string.Empty;
    public string? InvoiceTemplateCode { get; set; }
    public string? InvoiceSeries { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateOnly IssueDate { get; set; }
    public decimal RevenueExclVat { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string? Note { get; set; }
    public string InvoiceType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? SourceBatchId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; }
}

public sealed class Advance
{
    public Guid Id { get; set; }
    public string SellerTaxCode { get; set; } = string.Empty;
    public string CustomerTaxCode { get; set; } = string.Empty;
    public string? AdvanceNo { get; set; }
    public DateOnly AdvanceDate { get; set; }
    public decimal Amount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? SourceBatchId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; }
}
