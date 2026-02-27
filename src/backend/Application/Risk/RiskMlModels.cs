namespace CongNoGolden.Application.Risk;

public sealed record RiskMlTrainRequest(
    int? LookbackMonths,
    int? HorizonDays,
    bool? AutoActivate,
    int? MinSamples);

public sealed record RiskMlTrainResult(
    RiskMlTrainingRunSummary Run,
    RiskMlModelSummary? Model);

public sealed record RiskMlModelSummary(
    Guid Id,
    string ModelKey,
    int Version,
    string Algorithm,
    int HorizonDays,
    string Status,
    bool IsActive,
    DateTimeOffset TrainedAt,
    int TrainSampleCount,
    int ValidationSampleCount,
    decimal PositiveRatio,
    decimal? Accuracy,
    decimal? Auc,
    decimal? BrierScore);

public sealed record RiskMlTrainingRunSummary(
    Guid Id,
    string ModelKey,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int LookbackMonths,
    int HorizonDays,
    int SampleCount,
    int ValidationSampleCount,
    decimal PositiveRatio,
    decimal? Accuracy,
    decimal? Auc,
    decimal? BrierScore,
    string? Message,
    Guid? ModelId);

