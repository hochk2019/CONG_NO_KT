namespace CongNoGolden.Application.Reports;

public sealed record ReportDeliveryRunArtifact(
    string? FileName,
    string? ContentType,
    int? SizeBytes);

public sealed record ReportDeliveryRunItem(
    Guid Id,
    Guid ScheduleId,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string? ErrorDetail,
    ReportDeliveryRunArtifact? Artifact,
    DateTimeOffset CreatedAt);

public sealed record ReportDeliveryRunListRequest(
    int Page = 1,
    int PageSize = 20);
