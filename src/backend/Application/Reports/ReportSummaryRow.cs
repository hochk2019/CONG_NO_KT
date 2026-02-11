namespace CongNoGolden.Application.Reports;

public sealed class ReportSummaryRow
{
    public string GroupKey { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    public decimal InvoicedTotal { get; set; }
    public decimal AdvancedTotal { get; set; }
    public decimal ReceiptedTotal { get; set; }
    public decimal OutstandingInvoice { get; set; }
    public decimal OutstandingAdvance { get; set; }
    public decimal CurrentBalance { get; set; }
}
