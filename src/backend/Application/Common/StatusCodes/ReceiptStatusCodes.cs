namespace CongNoGolden.Application.Common.StatusCodes;

public static class ReceiptStatusCodes
{
    public const string Draft = "DRAFT";
    public const string Approved = "APPROVED";
    public const string Void = "VOID";
}

public static class ReceiptAllocationStatusCodes
{
    public const string Unallocated = "UNALLOCATED";
    public const string Selected = "SELECTED";
    public const string Suggested = "SUGGESTED";
    public const string Partial = "PARTIAL";
    public const string Allocated = "ALLOCATED";
}

public static class ReceiptHeldCreditStatusCodes
{
    public const string Holding = "HOLDING";
    public const string Partial = "PARTIAL";
    public const string Reapplied = "REAPPLIED";
    public const string Released = "RELEASED";
}
