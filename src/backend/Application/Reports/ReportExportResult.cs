namespace CongNoGolden.Application.Reports;

public sealed record ReportExportResult(
    byte[] Content,
    string FileName,
    string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
);
