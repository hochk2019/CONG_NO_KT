namespace CongNoGolden.Infrastructure.Services.RiskMl;

internal sealed record RiskSnapshotMetric(
    string CustomerTaxCode,
    decimal TotalOutstanding,
    decimal OverdueAmount,
    decimal OverdueRatio,
    int MaxDaysPastDue,
    int LateCount);

internal sealed record RiskTrainingSample(
    DateOnly SnapshotDate,
    string CustomerTaxCode,
    double[] Features,
    double Label);

internal sealed record LogisticRegressionModel(
    double Intercept,
    IReadOnlyList<double> Coefficients,
    IReadOnlyList<double> Means,
    IReadOnlyList<double> Scales,
    IReadOnlyList<string> FeatureNames);

internal sealed record LogisticTrainingMetrics(
    double Accuracy,
    double Precision,
    double Recall,
    double F1Score,
    double Auc,
    double BrierScore);

