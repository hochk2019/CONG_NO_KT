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
