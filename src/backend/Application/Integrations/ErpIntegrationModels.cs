namespace CongNoGolden.Application.Integrations;

public sealed record ErpIntegrationConfig(
    bool Enabled,
    string Provider,
    string? BaseUrl,
    string? CompanyCode,
    int TimeoutSeconds,
    bool HasApiKey,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy);

public sealed record ErpIntegrationConfigUpdateRequest(
    bool Enabled,
    string? Provider,
    string? BaseUrl,
    string? CompanyCode,
    int TimeoutSeconds,
    string? ApiKey,
    bool ClearApiKey);

public sealed record ErpIntegrationStatus(
    string Provider,
    bool Enabled,
    bool Configured,
    bool HasApiKey,
    string? BaseUrl,
    string? CompanyCode,
    int TimeoutSeconds,
    DateTimeOffset? LastSyncAtUtc,
    string? LastSyncStatus,
    string? LastSyncMessage);

public sealed record ErpSyncSummaryRequest(
    DateOnly? From,
    DateOnly? To,
    DateOnly? AsOfDate,
    int DueSoonDays,
    bool DryRun,
    string? RequestedBy);

public sealed record ErpSyncSummaryResult(
    bool Success,
    string Status,
    string Message,
    DateTimeOffset ExecutedAtUtc,
    string Provider,
    string? RequestId,
    ErpSyncSummaryPayload Payload);

public sealed record ErpSyncSummaryPayload(
    DateOnly? From,
    DateOnly? To,
    DateOnly? AsOfDate,
    int DueSoonDays,
    decimal TotalOutstanding,
    decimal OutstandingInvoice,
    decimal OutstandingAdvance,
    decimal UnallocatedReceiptsAmount,
    int UnallocatedReceiptsCount,
    decimal OverdueAmount,
    int OverdueCustomers,
    decimal DueSoonAmount,
    int DueSoonCustomers,
    int OnTimeCustomers);
