namespace CongNoGolden.Infrastructure.Data.Entities;

public sealed class RiskRule
{
    public Guid Id { get; set; }
    public string Level { get; set; } = string.Empty;
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
    public string? Message { get; set; }
    public string? ErrorDetail { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
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
