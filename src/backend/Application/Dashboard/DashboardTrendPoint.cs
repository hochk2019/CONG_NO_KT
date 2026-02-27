namespace CongNoGolden.Application.Dashboard;

public sealed record DashboardTrendPoint(
    string Period,
    decimal InvoicedTotal,
    decimal AdvancedTotal,
    decimal ReceiptedTotal,
    decimal ExpectedTotal,
    decimal ActualTotal,
    decimal Variance);
