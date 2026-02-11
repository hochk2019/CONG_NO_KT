namespace CongNoGolden.Application.Reports;

public sealed record ReportExportResult(
    byte[] Content,
    string FileName
);
