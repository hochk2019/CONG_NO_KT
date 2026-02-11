namespace CongNoGolden.Infrastructure.Data.Entities;

public sealed class Receipt
{
    public Guid Id { get; set; }
    public string SellerTaxCode { get; set; } = string.Empty;
    public string CustomerTaxCode { get; set; } = string.Empty;
    public string? ReceiptNo { get; set; }
    public DateOnly ReceiptDate { get; set; }
    public DateOnly? AppliedPeriodStart { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AllocationMode { get; set; } = string.Empty;
    public string AllocationStatus { get; set; } = "UNALLOCATED";
    public string AllocationPriority { get; set; } = "ISSUE_DATE";
    public string? AllocationTargets { get; set; }
    public string? AllocationSource { get; set; }
    public DateTimeOffset? AllocationSuggestedAt { get; set; }
    public decimal UnallocatedAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? SourceBatchId { get; set; }
    public DateTimeOffset? LastReminderAt { get; set; }
    public DateTimeOffset? ReminderDisabledAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; }
}

public sealed class ReceiptAllocation
{
    public Guid Id { get; set; }
    public Guid ReceiptId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public Guid? InvoiceId { get; set; }
    public Guid? AdvanceId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
