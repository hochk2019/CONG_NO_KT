namespace CongNoGolden.Infrastructure.Data.Entities;

public sealed class RiskRule
{
    public Guid Id { get; set; }
    public string Level { get; set; } = string.Empty;
    public string MatchMode { get; set; } = "ANY";
    public int MinOverdueDays { get; set; }
    public decimal MinOverdueRatio { get; set; }
    public int MinLateCount { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ReminderSetting
{
    public Guid Id { get; set; }
    public bool Singleton { get; set; }
    public bool Enabled { get; set; }
    public int FrequencyDays { get; set; }
    public int UpcomingDueDays { get; set; }
    public int EscalationMaxAttempts { get; set; } = 3;
    public int EscalationCooldownHours { get; set; } = 24;
    public int EscalateToSupervisorAfter { get; set; } = 2;
    public int EscalateToAdminAfter { get; set; } = 3;
    public string Channels { get; set; } = "[]";
    public string TargetLevels { get; set; } = "[]";
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ReminderLog
{
    public Guid Id { get; set; }
    public string CustomerTaxCode { get; set; } = string.Empty;
    public Guid? OwnerUserId { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int EscalationLevel { get; set; } = 1;
    public string? EscalationReason { get; set; }
    public string? Message { get; set; }
    public string? ErrorDetail { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ReminderResponseState
{
    public Guid Id { get; set; }
    public string CustomerTaxCode { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string ResponseStatus { get; set; } = "NO_RESPONSE";
    public DateTimeOffset? LatestResponseAt { get; set; }
    public bool EscalationLocked { get; set; }
    public int AttemptCount { get; set; }
    public int CurrentEscalationLevel { get; set; } = 1;
    public DateTimeOffset? LastSentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class NotificationPreference
{
    public Guid UserId { get; set; }
    public bool ReceiveNotifications { get; set; }
    public bool PopupEnabled { get; set; }
    public string PopupSeverities { get; set; } = "[]";
    public string PopupSources { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RiskMlModel
{
    public Guid Id { get; set; }
    public string ModelKey { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Algorithm { get; set; } = string.Empty;
    public int HorizonDays { get; set; }
    public string FeatureSchema { get; set; } = "{}";
    public string Parameters { get; set; } = "{}";
    public string Metrics { get; set; } = "{}";
    public int TrainSampleCount { get; set; }
    public int ValidationSampleCount { get; set; }
    public decimal PositiveRatio { get; set; }
    public bool IsActive { get; set; }
    public string Status { get; set; } = "TRAINED";
    public DateTimeOffset TrainedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RiskScoreSnapshot
{
    public Guid Id { get; set; }
    public string CustomerTaxCode { get; set; } = string.Empty;
    public DateOnly AsOfDate { get; set; }
    public decimal Score { get; set; }
    public string Signal { get; set; } = "LOW";
    public string? ModelVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class RiskDeltaAlert
{
    public Guid Id { get; set; }
    public string CustomerTaxCode { get; set; } = string.Empty;
    public DateOnly AsOfDate { get; set; }
    public decimal PrevScore { get; set; }
    public decimal CurrScore { get; set; }
    public decimal Delta { get; set; }
    public decimal Threshold { get; set; }
    public string Status { get; set; } = "OPEN";
    public DateTimeOffset DetectedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RiskMlTrainingRun
{
    public Guid Id { get; set; }
    public string ModelKey { get; set; } = string.Empty;
    public string Status { get; set; } = "RUNNING";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int LookbackMonths { get; set; }
    public int HorizonDays { get; set; }
    public int SampleCount { get; set; }
    public int ValidationSampleCount { get; set; }
    public decimal PositiveRatio { get; set; }
    public string? Metrics { get; set; }
    public string? Message { get; set; }
    public Guid? ModelId { get; set; }
    public Guid? CreatedBy { get; set; }
}
