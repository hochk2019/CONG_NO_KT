using CongNoGolden.Domain.Risk;

namespace CongNoGolden.Application.Risk;

public interface IRiskAiModelService
{
    RiskAiPrediction Predict(RiskMetrics metrics, DateOnly asOfDate);
    Task<RiskMlTrainResult> TrainAsync(RiskMlTrainRequest request, CancellationToken ct);
    Task<IReadOnlyList<RiskMlModelSummary>> ListModelsAsync(string? modelKey, int take, CancellationToken ct);
    Task<IReadOnlyList<RiskMlTrainingRunSummary>> ListTrainingRunsAsync(string? modelKey, int take, CancellationToken ct);
    Task<RiskMlModelSummary?> GetActiveModelAsync(string? modelKey, CancellationToken ct);
    Task<RiskMlModelSummary> ActivateModelAsync(Guid modelId, CancellationToken ct);
}

