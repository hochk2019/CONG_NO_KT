namespace CongNoGolden.Application.Reports;

public interface IReportExportService
{
    Task<ReportExportResult> ExportAsync(ReportExportRequest request, CancellationToken ct);
}
