using System.Text.Json.Serialization;

namespace CongNoGolden.Application.Collections;

public static class CollectionTaskStatusCodes
{
    public const string Open = "OPEN";
    public const string InProgress = "IN_PROGRESS";
    public const string Done = "DONE";
    public const string Cancelled = "CANCELLED";

    public static bool IsValid(string value) =>
        value is Open or InProgress or Done or Cancelled;
}

public sealed record CollectionTaskSnapshot(
    Guid TaskId,
    string CustomerTaxCode,
    string CustomerName,
    Guid? OwnerId,
    string? OwnerName,
    decimal TotalOutstanding,
    decimal OverdueAmount,
    int MaxDaysPastDue,
    decimal PredictedOverdueProbability,
    string RiskLevel,
    string AiSignal,
    decimal PriorityScore,
    string Status,
    Guid? AssignedTo,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt
);

public sealed record EnqueueCollectionTaskRequest(
    string CustomerTaxCode,
    string CustomerName,
    Guid? OwnerId,
    string? OwnerName,
    decimal TotalOutstanding,
    decimal OverdueAmount,
    int MaxDaysPastDue,
    decimal PredictedOverdueProbability,
    string RiskLevel,
    string AiSignal,
    decimal PriorityScore
);

public sealed record CollectionTaskListRequest(
    string? Status = null,
    Guid? AssignedTo = null,
    string? Search = null,
    int Take = 50
);

public sealed record CollectionTaskGenerateRequest(
    [property: JsonPropertyName("as_of_date")] string? AsOfDate = null,
    [property: JsonPropertyName("owner_id")] Guid? OwnerId = null,
    [property: JsonPropertyName("take")] int? Take = null,
    [property: JsonPropertyName("min_priority_score")] decimal? MinPriorityScore = null
);

public sealed record CollectionTaskAssignRequest(
    [property: JsonPropertyName("assigned_to")] Guid? AssignedTo
);

public sealed record CollectionTaskStatusUpdateRequest(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("note")] string? Note = null
);
