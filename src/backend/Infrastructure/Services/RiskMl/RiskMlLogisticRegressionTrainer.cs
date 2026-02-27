namespace CongNoGolden.Infrastructure.Services.RiskMl;

internal sealed class RiskMlLogisticRegressionTrainer
{
    private const double Epsilon = 1e-9;
    private readonly double _learningRate;
    private readonly int _maxIterations;
    private readonly double _l2Penalty;

    public RiskMlLogisticRegressionTrainer(double learningRate, int maxIterations, double l2Penalty)
    {
        _learningRate = learningRate <= 0 ? 0.08 : learningRate;
        _maxIterations = maxIterations <= 0 ? 800 : maxIterations;
        _l2Penalty = l2Penalty < 0 ? 0 : l2Penalty;
    }

    public LogisticRegressionModel Train(IReadOnlyList<RiskTrainingSample> samples)
    {
        if (samples.Count == 0)
        {
            throw new InvalidOperationException("Training dataset is empty.");
        }

        var featureCount = samples[0].Features.Length;
        if (featureCount == 0)
        {
            throw new InvalidOperationException("Feature vector is empty.");
        }

        var means = new double[featureCount];
        var scales = new double[featureCount];
        ComputeFeatureScaling(samples, means, scales);

        var coefficients = new double[featureCount];
        var gradient = new double[featureCount];
        var normalized = new double[featureCount];
        var intercept = 0d;
        var previousLoss = double.MaxValue;

        for (var iteration = 0; iteration < _maxIterations; iteration++)
        {
            Array.Clear(gradient, 0, gradient.Length);
            var gradIntercept = 0d;
            var loss = 0d;

            for (var i = 0; i < samples.Count; i++)
            {
                var sample = samples[i];
                Normalize(sample.Features, means, scales, normalized);
                var probability = Sigmoid(Dot(coefficients, normalized) + intercept);
                var label = sample.Label;
                var error = probability - label;

                gradIntercept += error;
                for (var j = 0; j < featureCount; j++)
                {
                    gradient[j] += error * normalized[j];
                }

                loss += -label * Math.Log(probability + Epsilon)
                    - (1d - label) * Math.Log(1d - probability + Epsilon);
            }

            var sampleCount = samples.Count;
            var step = _learningRate / sampleCount;
            intercept -= step * gradIntercept;

            for (var j = 0; j < featureCount; j++)
            {
                var l2Gradient = _l2Penalty * coefficients[j];
                coefficients[j] -= _learningRate * ((gradient[j] / sampleCount) + l2Gradient);
            }

            var avgLoss = loss / sampleCount;
            if (Math.Abs(previousLoss - avgLoss) < 1e-7)
            {
                break;
            }

            previousLoss = avgLoss;
        }

        return new LogisticRegressionModel(
            intercept,
            coefficients,
            means,
            scales,
            RiskMlFeatureEngineering.FeatureNames);
    }

    public static double PredictProbability(LogisticRegressionModel model, IReadOnlyList<double> features)
    {
        if (features.Count != model.Coefficients.Count)
        {
            throw new InvalidOperationException("Feature vector length does not match model coefficients.");
        }

        var normalized = new double[features.Count];
        for (var i = 0; i < features.Count; i++)
        {
            var scale = model.Scales[i];
            var mean = model.Means[i];
            normalized[i] = scale <= Epsilon ? features[i] - mean : (features[i] - mean) / scale;
        }

        return Sigmoid(Dot(model.Coefficients, normalized) + model.Intercept);
    }

    public static LogisticTrainingMetrics Evaluate(LogisticRegressionModel model, IReadOnlyList<RiskTrainingSample> samples)
    {
        if (samples.Count == 0)
        {
            return new LogisticTrainingMetrics(0, 0, 0, 0, 0, 0);
        }

        var truePositive = 0;
        var trueNegative = 0;
        var falsePositive = 0;
        var falseNegative = 0;
        var brier = 0d;

        var scores = new List<(double Probability, int Label)>(samples.Count);
        for (var i = 0; i < samples.Count; i++)
        {
            var probability = PredictProbability(model, samples[i].Features);
            var label = samples[i].Label >= 0.5d ? 1 : 0;
            scores.Add((probability, label));

            var predicted = probability >= 0.5d ? 1 : 0;
            if (predicted == 1 && label == 1)
            {
                truePositive++;
            }
            else if (predicted == 0 && label == 0)
            {
                trueNegative++;
            }
            else if (predicted == 1)
            {
                falsePositive++;
            }
            else
            {
                falseNegative++;
            }

            var error = probability - label;
            brier += error * error;
        }

        var total = samples.Count;
        var accuracy = (truePositive + trueNegative) / (double)total;
        var precision = truePositive + falsePositive == 0
            ? 0d
            : truePositive / (double)(truePositive + falsePositive);
        var recall = truePositive + falseNegative == 0
            ? 0d
            : truePositive / (double)(truePositive + falseNegative);
        var f1 = precision + recall <= Epsilon
            ? 0d
            : (2d * precision * recall) / (precision + recall);
        var auc = ComputeAuc(scores);
        var brierScore = brier / total;

        return new LogisticTrainingMetrics(
            accuracy,
            precision,
            recall,
            f1,
            auc,
            brierScore);
    }

    private static void ComputeFeatureScaling(
        IReadOnlyList<RiskTrainingSample> samples,
        double[] means,
        double[] scales)
    {
        var sampleCount = samples.Count;
        for (var i = 0; i < sampleCount; i++)
        {
            var features = samples[i].Features;
            for (var j = 0; j < features.Length; j++)
            {
                means[j] += features[j];
            }
        }

        for (var j = 0; j < means.Length; j++)
        {
            means[j] /= sampleCount;
        }

        for (var i = 0; i < sampleCount; i++)
        {
            var features = samples[i].Features;
            for (var j = 0; j < features.Length; j++)
            {
                var diff = features[j] - means[j];
                scales[j] += diff * diff;
            }
        }

        for (var j = 0; j < scales.Length; j++)
        {
            scales[j] = Math.Sqrt(scales[j] / sampleCount);
            if (scales[j] < Epsilon)
            {
                scales[j] = 1d;
            }
        }
    }

    private static void Normalize(
        IReadOnlyList<double> features,
        IReadOnlyList<double> means,
        IReadOnlyList<double> scales,
        double[] output)
    {
        for (var i = 0; i < features.Count; i++)
        {
            output[i] = (features[i] - means[i]) / scales[i];
        }
    }

    private static double Dot(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        var sum = 0d;
        for (var i = 0; i < left.Count; i++)
        {
            sum += left[i] * right[i];
        }

        return sum;
    }

    private static double Sigmoid(double value)
    {
        if (value >= 35d)
        {
            return 1d;
        }

        if (value <= -35d)
        {
            return 0d;
        }

        return 1d / (1d + Math.Exp(-value));
    }

    private static double ComputeAuc(IReadOnlyList<(double Probability, int Label)> scores)
    {
        var positives = scores.Count(s => s.Label == 1);
        var negatives = scores.Count - positives;
        if (positives == 0 || negatives == 0)
        {
            return 0.5d;
        }

        var ordered = scores
            .OrderBy(s => s.Probability)
            .ToList();

        var rank = 1;
        var positiveRankSum = 0d;
        var i = 0;
        while (i < ordered.Count)
        {
            var j = i + 1;
            while (j < ordered.Count && Math.Abs(ordered[j].Probability - ordered[i].Probability) <= Epsilon)
            {
                j++;
            }

            var groupSize = j - i;
            var averageRank = (rank + (rank + groupSize - 1)) / 2d;
            for (var k = i; k < j; k++)
            {
                if (ordered[k].Label == 1)
                {
                    positiveRankSum += averageRank;
                }
            }

            rank += groupSize;
            i = j;
        }

        var auc = (positiveRankSum - (positives * (positives + 1) / 2d)) / (positives * negatives);
        return Math.Clamp(auc, 0d, 1d);
    }
}

