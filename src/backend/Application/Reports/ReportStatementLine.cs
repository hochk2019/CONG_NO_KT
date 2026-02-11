namespace CongNoGolden.Application.Reports;

public sealed class ReportStatementLine
{
    public DateOnly DocumentDate { get; set; }
    public DateOnly? AppliedPeriodStart { get; set; }
    public string Type { get; set; } = string.Empty;
    public string SellerTaxCode { get; set; } = string.Empty;
    public string CustomerTaxCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? DocumentNo { get; set; }
    public string? Description { get; set; }
    public decimal Revenue { get; set; }
    public decimal Vat { get; set; }
    public decimal Increase { get; set; }
    public decimal Decrease { get; set; }
    public decimal RunningBalance { get; set; }
    public string? CreatedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public string? Batch { get; set; }
}
