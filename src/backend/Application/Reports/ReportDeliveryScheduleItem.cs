namespace CongNoGolden.Application.Reports;

public sealed record ReportDeliveryFilterDto(
    DateOnly? From,
    DateOnly? To,
    DateOnly? AsOfDate,
    string? SellerTaxCode,
    string? CustomerTaxCode,
    Guid? OwnerId,
    string? FilterText);

public sealed record ReportDeliveryScheduleItem(
    Guid Id,
    Guid UserId,
    ReportExportKind ReportKind,
    ReportExportFormat ReportFormat,
    string CronExpression,
    string TimezoneId,
    IReadOnlyList<string> Recipients,
    ReportDeliveryFilterDto Filter,
    bool Enabled,
    DateTimeOffset? LastRunAt,
    DateTimeOffset? NextRunAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? LastRunStatus,
    DateTimeOffset? LastRunFinishedAt);

public sealed record ReportDeliveryScheduleUpsertRequest(
    ReportExportKind ReportKind,
    ReportExportFormat ReportFormat,
    string CronExpression,
    string? TimezoneId,
    IReadOnlyList<string>? Recipients,
    ReportDeliveryFilterDto? Filter,
    bool Enabled);
