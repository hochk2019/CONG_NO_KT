using CongNoGolden.Infrastructure.Services.RiskMl;
using Xunit;

namespace Tests.Unit;

public sealed class RiskMlLogisticRegressionTrainerTests
{
    [Fact]
    public void Train_LearnsSignal_FromLabeledSamples()
    {
        var random = new Random(42);
        var samples = new List<RiskTrainingSample>(800);
        for (var i = 0; i < 800; i++)
        {
            var snapshot = new DateOnly(2025, (i % 12) + 1, (i % 28) + 1);
            var monthAngle = 2d * Math.PI * ((snapshot.Month - 1d) / 12d);
            var weekdayAngle = 2d * Math.PI * ((int)snapshot.DayOfWeek / 7d);

            var f1 = random.NextDouble() * 2d - 1d;
            var f2 = random.NextDouble() * 2d - 1d;
            var f3 = random.NextDouble();
            var f4 = random.NextDouble() * 3d;
            var f5 = random.NextDouble() * 2d;
            var f6 = Math.Sin(monthAngle);
            var f7 = Math.Cos(monthAngle);
            var f8 = Math.Sin(weekdayAngle);
            var f9 = Math.Cos(weekdayAngle);

            var logit = (2.1d * f1) - (1.4d * f2) + (1.2d * f3) + (0.6d * f6) - 0.3d;
            var probability = 1d / (1d + Math.Exp(-logit));
            var label = random.NextDouble() < probability ? 1d : 0d;

            samples.Add(new RiskTrainingSample(
                snapshot,
                $"C{i:0000}",
                [f1, f2, f3, f4, f5, f6, f7, f8, f9],
                label));
        }

        var trainSet = samples.Take(640).ToList();
        var validationSet = samples.Skip(640).ToList();
        var trainer = new RiskMlLogisticRegressionTrainer(
            learningRate: 0.10d,
            maxIterations: 1200,
            l2Penalty: 0.01d);

        var model = trainer.Train(trainSet);
        var metrics = RiskMlLogisticRegressionTrainer.Evaluate(model, validationSet);

        Assert.InRange(metrics.Accuracy, 0.65d, 1.00d);
        Assert.InRange(metrics.Auc, 0.70d, 1.00d);
    }
}

