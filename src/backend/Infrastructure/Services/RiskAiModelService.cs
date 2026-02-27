using System.Text.Json;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Risk;
using CongNoGolden.Domain.Risk;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services.RiskMl;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Infrastructure.Services;

public sealed class RiskModelTrainingOptions
{
    public bool AutoRunEnabled { get; set; } = true;
    public int PollMinutes { get; set; } = 1440;
    public string ModelKey { get; set; } = RiskMlFeatureEngineering.ModelKey;
    public int LookbackMonths { get; set; } = 12;
    public int HorizonDays { get; set; } = 30;
    public int MinSamples { get; set; } = 200;
    public bool AutoActivate { get; set; } = true;
    public decimal MinAucGain { get; set; } = 0.005m;
    public double LearningRate { get; set; } = 0.08d;
    public int MaxIterations { get; set; } = 900;
    public double L2Penalty { get; set; } = 0.02d;
}

public sealed class RiskAiModelService : IRiskAiModelService
{
    private static readonly JsonSerializerOptions ModelJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ConGNoDbContext _db;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ICurrentUser _currentUser;
    private readonly RiskModelTrainingOptions _options;
    private readonly ILogger<RiskAiModelService> _logger;
    private readonly object _runtimeLock = new();
    private string? _runtimeModelKey;
    private LogisticRegressionModel? _runtimeModel;

    public RiskAiModelService(
        ConGNoDbContext db,
        IDbConnectionFactory connectionFactory,
        ICurrentUser currentUser,
        IOptions<RiskModelTrainingOptions> options,
        ILogger<RiskAiModelService> logger)
    {
        _db = db;
        _connectionFactory = connectionFactory;
        _currentUser = currentUser;
        _logger = logger;
        _options = options.Value;
    }

    public RiskAiPrediction Predict(RiskMetrics metrics, DateOnly asOfDate)
    {
        var runtime = GetRuntimeSnapshot(_options.ModelKey);
        if (runtime is null)
        {
            return RiskAiScorer.Predict(metrics);
        }

        var features = RiskMlFeatureEngineering.BuildFeatureVector(metrics, asOfDate);
        var probability = RiskMlLogisticRegressionTrainer.PredictProbability(runtime, features);
        var rounded = Math.Round((decimal)probability, 4, MidpointRounding.AwayFromZero);
        var signal = RiskMlFeatureEngineering.ResolveSignal(rounded);

        return new RiskAiPrediction(
            rounded,
            signal,
            RiskAiScorer.BuildFactors(metrics),
            RiskAiScorer.ResolveRecommendation(signal));
    }

    public async Task<RiskMlTrainResult> TrainAsync(RiskMlTrainRequest request, CancellationToken ct)
    {
        var modelKey = NormalizeModelKey(_options.ModelKey);
        var lookbackMonths = NormalizePositive(request.LookbackMonths, _options.LookbackMonths, 3, 36);
        var horizonDays = NormalizePositive(request.HorizonDays, _options.HorizonDays, 7, 120);
        var minSamples = NormalizePositive(request.MinSamples, _options.MinSamples, 50, 1_000_000);
        var autoActivate = request.AutoActivate ?? _options.AutoActivate;

        var run = new RiskMlTrainingRun
        {
            Id = Guid.NewGuid(),
            ModelKey = modelKey,
            Status = "RUNNING",
            StartedAt = DateTimeOffset.UtcNow,
            LookbackMonths = lookbackMonths,
            HorizonDays = horizonDays,
            CreatedBy = _currentUser.UserId
        };

        _db.RiskMlTrainingRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        try
        {
            var dataset = await BuildTrainingDatasetAsync(lookbackMonths, horizonDays, ct);
            var sampleCount = dataset.Count;
            var positiveCount = dataset.Count(s => s.Label >= 0.5d);
            var positiveRatio = sampleCount == 0 ? 0m : Math.Round((decimal)positiveCount / sampleCount, 6);

            if (sampleCount < minSamples || positiveCount == 0 || positiveCount == sampleCount)
            {
                run.Status = "SKIPPED";
                run.FinishedAt = DateTimeOffset.UtcNow;
                run.SampleCount = sampleCount;
                run.ValidationSampleCount = 0;
                run.PositiveRatio = positiveRatio;
                run.Message = sampleCount < minSamples
                    ? $"Insufficient samples ({sampleCount} < {minSamples})."
                    : "Dataset must contain both positive and negative labels.";
                await _db.SaveChangesAsync(ct);

                _logger.LogWarning("ML training skipped: {Reason}", run.Message);
                return new RiskMlTrainResult(ToRunSummary(run), null);
            }

            var (trainSet, validationSet) = SplitDataset(dataset);
            var trainer = new RiskMlLogisticRegressionTrainer(
                _options.LearningRate,
                _options.MaxIterations,
                _options.L2Penalty);
            var modelRuntime = trainer.Train(trainSet);
            var metrics = RiskMlLogisticRegressionTrainer.Evaluate(modelRuntime, validationSet);
            var now = DateTimeOffset.UtcNow;
            var nextVersion = (await _db.RiskMlModels
                    .Where(m => m.ModelKey == modelKey)
                    .MaxAsync(m => (int?)m.Version, ct) ?? 0) + 1;

            var activeModel = await _db.RiskMlModels
                .Where(m => m.ModelKey == modelKey && m.IsActive)
                .FirstOrDefaultAsync(ct);

            var shouldActivate = autoActivate && ShouldActivate(activeModel, metrics.Auc);
            if (shouldActivate)
            {
                var activeRows = await _db.RiskMlModels
                    .Where(m => m.ModelKey == modelKey && m.IsActive)
                    .ToListAsync(ct);
                foreach (var row in activeRows)
                {
                    row.IsActive = false;
                    if (row.Status == "ACTIVE")
                    {
                        row.Status = "TRAINED";
                    }

                    row.UpdatedAt = now;
                }
            }

            var model = new RiskMlModel
            {
                Id = Guid.NewGuid(),
                ModelKey = modelKey,
                Version = nextVersion,
                Algorithm = "logistic_regression_v1",
                HorizonDays = horizonDays,
                FeatureSchema = SerializeFeatureSchema(horizonDays),
                Parameters = SerializeParameters(modelRuntime),
                Metrics = SerializeMetrics(metrics),
                TrainSampleCount = trainSet.Count,
                ValidationSampleCount = validationSet.Count,
                PositiveRatio = positiveRatio,
                IsActive = shouldActivate,
                Status = shouldActivate ? "ACTIVE" : "TRAINED",
                TrainedAt = now,
                CreatedBy = _currentUser.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.RiskMlModels.Add(model);

            run.Status = "SUCCEEDED";
            run.FinishedAt = now;
            run.SampleCount = sampleCount;
            run.ValidationSampleCount = validationSet.Count;
            run.PositiveRatio = positiveRatio;
            run.Metrics = model.Metrics;
            run.ModelId = model.Id;
            run.Message = shouldActivate
                ? $"Model v{model.Version} trained and activated."
                : $"Model v{model.Version} trained.";

            await _db.SaveChangesAsync(ct);

            if (shouldActivate)
            {
                SetRuntime(model.ModelKey, modelRuntime);
            }

            _logger.LogInformation(
                "ML training succeeded. ModelKey={ModelKey} Version={Version} Active={Active} AUC={Auc:F4} Samples={Samples}",
                model.ModelKey,
                model.Version,
                model.IsActive,
                metrics.Auc,
                sampleCount);

            return new RiskMlTrainResult(
                ToRunSummary(run),
                ToModelSummary(model));
        }
        catch (Exception ex)
        {
            run.Status = "FAILED";
            run.FinishedAt = DateTimeOffset.UtcNow;
            run.Message = ex.Message;
            await _db.SaveChangesAsync(ct);
            _logger.LogError(ex, "ML training failed.");
            throw;
        }
    }

    public async Task<IReadOnlyList<RiskMlModelSummary>> ListModelsAsync(string? modelKey, int take, CancellationToken ct)
    {
        var key = string.IsNullOrWhiteSpace(modelKey) ? null : NormalizeModelKey(modelKey);
        var limit = NormalizePositive(take, 20, 1, 200);

        var query = _db.RiskMlModels.AsNoTracking();
        if (key is not null)
        {
            query = query.Where(m => m.ModelKey == key);
        }

        var rows = await query
            .OrderByDescending(m => m.TrainedAt)
            .Take(limit)
            .ToListAsync(ct);

        return rows.Select(ToModelSummary).ToList();
    }

    public async Task<IReadOnlyList<RiskMlTrainingRunSummary>> ListTrainingRunsAsync(string? modelKey, int take, CancellationToken ct)
    {
        var key = string.IsNullOrWhiteSpace(modelKey) ? null : NormalizeModelKey(modelKey);
        var limit = NormalizePositive(take, 20, 1, 200);

        var query = _db.RiskMlTrainingRuns.AsNoTracking();
        if (key is not null)
        {
            query = query.Where(r => r.ModelKey == key);
        }

        var rows = await query
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .ToListAsync(ct);

        return rows.Select(ToRunSummary).ToList();
    }

    public async Task<RiskMlModelSummary?> GetActiveModelAsync(string? modelKey, CancellationToken ct)
    {
        var key = string.IsNullOrWhiteSpace(modelKey)
            ? NormalizeModelKey(_options.ModelKey)
            : NormalizeModelKey(modelKey);

        var activeModel = await _db.RiskMlModels.AsNoTracking()
            .Where(m => m.ModelKey == key && m.IsActive)
            .OrderByDescending(m => m.TrainedAt)
            .FirstOrDefaultAsync(ct);

        if (activeModel is null)
        {
            SetRuntime(key, null);
            return null;
        }

        SetRuntime(key, DeserializeParameters(activeModel.Parameters));
        return ToModelSummary(activeModel);
    }

    public async Task<RiskMlModelSummary> ActivateModelAsync(Guid modelId, CancellationToken ct)
    {
        var model = await _db.RiskMlModels
            .FirstOrDefaultAsync(m => m.Id == modelId, ct)
            ?? throw new InvalidOperationException("Risk ML model not found.");

        var sameKeyModels = await _db.RiskMlModels
            .Where(m => m.ModelKey == model.ModelKey && m.IsActive && m.Id != modelId)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var row in sameKeyModels)
        {
            row.IsActive = false;
            if (row.Status == "ACTIVE")
            {
                row.Status = "TRAINED";
            }

            row.UpdatedAt = now;
        }

        model.IsActive = true;
        model.Status = "ACTIVE";
        model.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        SetRuntime(model.ModelKey, DeserializeParameters(model.Parameters));
        return ToModelSummary(model);
    }

    private static string NormalizeModelKey(string modelKey)
    {
        var normalized = (modelKey ?? string.Empty).Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized)
            ? RiskMlFeatureEngineering.ModelKey
            : normalized;
    }

    private static int NormalizePositive(int? value, int fallback, int min, int max)
    {
        var candidate = value ?? fallback;
        if (candidate < min)
        {
            return min;
        }

        if (candidate > max)
        {
            return max;
        }

        return candidate;
    }

    private static (List<RiskTrainingSample> Train, List<RiskTrainingSample> Validation) SplitDataset(
        IReadOnlyList<RiskTrainingSample> samples)
    {
        var ordered = samples
            .OrderBy(s => s.SnapshotDate)
            .ThenBy(s => s.CustomerTaxCode, StringComparer.Ordinal)
            .ToList();

        var validationCount = Math.Clamp(ordered.Count / 5, 1, Math.Max(1, ordered.Count - 1));
        var trainCount = ordered.Count - validationCount;
        if (trainCount <= 0)
        {
            trainCount = ordered.Count - 1;
            validationCount = 1;
        }

        return (
            ordered.Take(trainCount).ToList(),
            ordered.Skip(trainCount).Take(validationCount).ToList());
    }

    private bool ShouldActivate(RiskMlModel? activeModel, double newAuc)
    {
        if (activeModel is null)
        {
            return true;
        }

        var currentAuc = TryReadMetric(activeModel.Metrics, "auc");
        if (!currentAuc.HasValue)
        {
            return true;
        }

        var minGain = Math.Max(0d, (double)_options.MinAucGain);
        return newAuc >= (double)currentAuc.Value + minGain;
    }

    private async Task<List<RiskTrainingSample>> BuildTrainingDatasetAsync(
        int lookbackMonths,
        int horizonDays,
        CancellationToken ct)
    {
        var snapshots = BuildSnapshotDates(lookbackMonths, horizonDays);
        var samples = new List<RiskTrainingSample>(lookbackMonths * 500);
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        foreach (var snapshot in snapshots)
        {
            var current = await LoadSnapshotAsync(connection, snapshot, ct);
            if (current.Count == 0)
            {
                continue;
            }

            var future = await LoadSnapshotAsync(connection, snapshot.AddDays(horizonDays), ct);
            foreach (var featureRow in current.Values)
            {
                var label = future.TryGetValue(featureRow.CustomerTaxCode, out var target) && target.OverdueAmount > 0m
                    ? 1d
                    : 0d;
                var metrics = new RiskMetrics(
                    featureRow.TotalOutstanding,
                    featureRow.OverdueAmount,
                    featureRow.OverdueRatio,
                    featureRow.MaxDaysPastDue,
                    featureRow.LateCount);
                var features = RiskMlFeatureEngineering.BuildFeatureVector(metrics, snapshot);
                samples.Add(new RiskTrainingSample(snapshot, featureRow.CustomerTaxCode, features, label));
            }
        }

        return samples;
    }

    private static List<DateOnly> BuildSnapshotDates(int lookbackMonths, int horizonDays)
    {
        var latestSnapshot = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-horizonDays));
        var monthAnchor = new DateOnly(latestSnapshot.Year, latestSnapshot.Month, 1);
        var snapshots = new List<DateOnly>(lookbackMonths);
        for (var i = lookbackMonths - 1; i >= 0; i--)
        {
            var monthStart = monthAnchor.AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            snapshots.Add(monthEnd);
        }

        return snapshots;
    }

    private static async Task<Dictionary<string, RiskSnapshotMetric>> LoadSnapshotAsync(
        System.Data.Common.DbConnection connection,
        DateOnly asOf,
        CancellationToken ct)
    {
        var rows = (await connection.QueryAsync<RiskSnapshotRow>(
            new CommandDefinition(
                SnapshotSql,
                new { asOf },
                cancellationToken: ct)))
            .Where(r => !string.IsNullOrWhiteSpace(r.CustomerTaxCode))
            .ToList();

        return rows
            .GroupBy(r => r.CustomerTaxCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var row = g.First();
                    return new RiskSnapshotMetric(
                        row.CustomerTaxCode!,
                        row.TotalOutstanding,
                        row.OverdueAmount,
                        row.OverdueRatio,
                        row.MaxDaysPastDue,
                        row.LateCount);
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private static string SerializeFeatureSchema(int horizonDays)
    {
        var payload = new
        {
            features = RiskMlFeatureEngineering.FeatureNames,
            label = "has_overdue_after_horizon",
            horizonDays
        };
        return JsonSerializer.Serialize(payload);
    }

    private static string SerializeParameters(LogisticRegressionModel model)
    {
        var payload = new
        {
            intercept = model.Intercept,
            coefficients = model.Coefficients,
            means = model.Means,
            scales = model.Scales,
            featureNames = model.FeatureNames
        };
        return JsonSerializer.Serialize(payload);
    }

    private static string SerializeMetrics(LogisticTrainingMetrics metrics)
    {
        var payload = new
        {
            accuracy = RoundMetric(metrics.Accuracy),
            precision = RoundMetric(metrics.Precision),
            recall = RoundMetric(metrics.Recall),
            f1 = RoundMetric(metrics.F1Score),
            auc = RoundMetric(metrics.Auc),
            brierScore = RoundMetric(metrics.BrierScore)
        };
        return JsonSerializer.Serialize(payload);
    }

    private static LogisticRegressionModel? DeserializeParameters(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            var model = JsonSerializer.Deserialize<ParameterPayload>(payload, ModelJsonOptions);
            if (model is null ||
                model.Coefficients is null ||
                model.Means is null ||
                model.Scales is null ||
                model.FeatureNames is null ||
                model.Coefficients.Length == 0 ||
                model.Coefficients.Length != model.Means.Length ||
                model.Coefficients.Length != model.Scales.Length)
            {
                return null;
            }

            return new LogisticRegressionModel(
                model.Intercept,
                model.Coefficients,
                model.Means,
                model.Scales,
                model.FeatureNames);
        }
        catch
        {
            return null;
        }
    }

    private static decimal RoundMetric(double value)
    {
        return Math.Round((decimal)value, 6, MidpointRounding.AwayFromZero);
    }

    private static decimal? TryReadMetric(string json, string metricName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(metricName, out var property))
            {
                return null;
            }

            if (property.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            return property.GetDecimal();
        }
        catch
        {
            return null;
        }
    }

    private RiskMlModelSummary ToModelSummary(RiskMlModel model)
    {
        return new RiskMlModelSummary(
            model.Id,
            model.ModelKey,
            model.Version,
            model.Algorithm,
            model.HorizonDays,
            model.Status,
            model.IsActive,
            model.TrainedAt,
            model.TrainSampleCount,
            model.ValidationSampleCount,
            model.PositiveRatio,
            TryReadMetric(model.Metrics, "accuracy"),
            TryReadMetric(model.Metrics, "auc"),
            TryReadMetric(model.Metrics, "brierScore"));
    }

    private RiskMlTrainingRunSummary ToRunSummary(RiskMlTrainingRun run)
    {
        return new RiskMlTrainingRunSummary(
            run.Id,
            run.ModelKey,
            run.Status,
            run.StartedAt,
            run.FinishedAt,
            run.LookbackMonths,
            run.HorizonDays,
            run.SampleCount,
            run.ValidationSampleCount,
            run.PositiveRatio,
            run.Metrics is null ? null : TryReadMetric(run.Metrics, "accuracy"),
            run.Metrics is null ? null : TryReadMetric(run.Metrics, "auc"),
            run.Metrics is null ? null : TryReadMetric(run.Metrics, "brierScore"),
            run.Message,
            run.ModelId);
    }

    private LogisticRegressionModel? GetRuntimeSnapshot(string modelKey)
    {
        lock (_runtimeLock)
        {
            if (_runtimeModel is null)
            {
                return null;
            }

            if (!string.Equals(_runtimeModelKey, modelKey, StringComparison.Ordinal))
            {
                return null;
            }

            return _runtimeModel;
        }
    }

    private void SetRuntime(string modelKey, LogisticRegressionModel? model)
    {
        lock (_runtimeLock)
        {
            _runtimeModelKey = modelKey;
            _runtimeModel = model;
        }
    }

    private sealed class RiskSnapshotRow
    {
        public string? CustomerTaxCode { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal OverdueAmount { get; set; }
        public decimal OverdueRatio { get; set; }
        public int MaxDaysPastDue { get; set; }
        public int LateCount { get; set; }
    }

    private sealed class ParameterPayload
    {
        public double Intercept { get; init; }
        public double[]? Coefficients { get; init; }
        public double[]? Means { get; init; }
        public double[]? Scales { get; init; }
        public string[]? FeatureNames { get; init; }
    }

    private const string SnapshotSql = @"
WITH invoice_alloc AS (
    SELECT ra.invoice_id, SUM(ra.amount) AS allocated
    FROM congno.receipt_allocations ra
    JOIN congno.receipts r ON r.id = ra.receipt_id
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND r.receipt_date <= @asOf
    GROUP BY ra.invoice_id
),
advance_alloc AS (
    SELECT ra.advance_id, SUM(ra.amount) AS allocated
    FROM congno.receipt_allocations ra
    JOIN congno.receipts r ON r.id = ra.receipt_id
    WHERE r.deleted_at IS NULL
      AND r.status = 'APPROVED'
      AND r.receipt_date <= @asOf
    GROUP BY ra.advance_id
),
invoice_out AS (
    SELECT i.customer_tax_code,
           (i.total_amount - COALESCE(a.allocated, 0)) AS outstanding,
           (i.issue_date + (COALESCE(c.payment_terms_days, 0) || ' days')::interval)::date AS due_date
    FROM congno.invoices i
    JOIN congno.customers c ON c.tax_code = i.customer_tax_code
    LEFT JOIN invoice_alloc a ON a.invoice_id = i.id
    WHERE i.deleted_at IS NULL
      AND i.status <> 'VOID'
),
advance_out AS (
    SELECT a.customer_tax_code,
           (a.amount - COALESCE(alloc.allocated, 0)) AS outstanding,
           (a.advance_date + (COALESCE(c.payment_terms_days, 0) || ' days')::interval)::date AS due_date
    FROM congno.advances a
    JOIN congno.customers c ON c.tax_code = a.customer_tax_code
    LEFT JOIN advance_alloc alloc ON alloc.advance_id = a.id
    WHERE a.deleted_at IS NULL
      AND a.status IN ('APPROVED','PAID')
),
combined AS (
    SELECT * FROM invoice_out
    UNION ALL
    SELECT * FROM advance_out
),
bucketed AS (
    SELECT customer_tax_code,
           outstanding,
           GREATEST(0, (@asOf::date - due_date))::int AS days_past_due
    FROM combined
    WHERE outstanding > 0
),
agg AS (
    SELECT customer_tax_code,
           SUM(outstanding) AS total_outstanding,
           SUM(CASE WHEN days_past_due > 0 THEN outstanding ELSE 0 END) AS overdue_amount,
           MAX(days_past_due) AS max_days_past_due,
           COUNT(*) FILTER (WHERE days_past_due > 0) AS late_count
    FROM bucketed
    GROUP BY customer_tax_code
)
SELECT customer_tax_code AS customerTaxCode,
       total_outstanding AS totalOutstanding,
       overdue_amount AS overdueAmount,
       COALESCE(overdue_amount / NULLIF(total_outstanding, 0), 0) AS overdueRatio,
       max_days_past_due AS maxDaysPastDue,
       late_count AS lateCount
FROM agg
WHERE total_outstanding > 0;
";
}
