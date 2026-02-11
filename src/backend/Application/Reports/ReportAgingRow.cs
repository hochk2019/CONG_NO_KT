namespace CongNoGolden.Application.Reports;

public sealed class ReportAgingRow
{
    public string CustomerTaxCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string SellerTaxCode { get; set; } = string.Empty;
    public decimal Bucket0To30 { get; set; }
    public decimal Bucket31To60 { get; set; }
    public decimal Bucket61To90 { get; set; }
    public decimal Bucket91To180 { get; set; }
    public decimal BucketOver180 { get; set; }
    public decimal Total { get; set; }
    public decimal Overdue { get; set; }
}
