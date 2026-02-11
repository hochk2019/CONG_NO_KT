namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReportExportService
{
    private sealed class ExportSummaryRow
    {
        public string CustomerTaxCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? OwnerName { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal InvoicedTotal { get; set; }
        public decimal AdvancedTotal { get; set; }
        public decimal ReceiptedTotal { get; set; }
        public decimal Adjustments { get; set; }
        public decimal ClosingBalance { get; set; }
        public decimal OverdueAmount { get; set; }
        public int MaxAgeDays { get; set; }
    }
}
