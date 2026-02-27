namespace CongNoGolden.Application.Reports;

public sealed record ReportExportRequest(
    DateOnly? From,
    DateOnly? To,
    DateOnly? AsOfDate,
    string? SellerTaxCode,
    string? CustomerTaxCode,
    Guid? OwnerId,
    string? FilterText,
    ReportExportKind Kind = ReportExportKind.Full,
    ReportExportFormat Format = ReportExportFormat.Xlsx
);
