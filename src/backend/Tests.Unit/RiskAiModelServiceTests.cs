using System.Data.Common;
using System.Text.Json;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Domain.Risk;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using CongNoGolden.Infrastructure.Services.RiskMl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Tests.Unit;

public sealed class RiskAiModelServiceTests
{
    [Fact]
    public void Predict_FallsBackToHeuristic_WhenNoActiveModel()
    {
        using var db = CreateDbContext(nameof(Predict_FallsBackToHeuristic_WhenNoActiveModel));
        var service = CreateService(db);

        var metrics = new RiskMetrics(
            TotalOutstanding: 120_000_000m,
            OverdueAmount: 40_000_000m,
            OverdueRatio: 0.33m,
            MaxDaysPastDue: 18,
            LateCount: 2);

        var baseline = RiskAiScorer.Predict(metrics);
        var predicted = service.Predict(metrics, new DateOnly(2026, 2, 12));

        Assert.Equal(baseline.Probability, predicted.Probability);
        Assert.Equal(baseline.Signal, predicted.Signal);
        Assert.Equal(baseline.Recommendation, predicted.Recommendation);
        Assert.Equal(baseline.Factors.Count, predicted.Factors.Count);
    }

    [Fact]
    public async Task Predict_UsesActiveModel_WhenAvailable()
    {
        await using var db = CreateDbContext(nameof(Predict_UsesActiveModel_WhenAvailable));
        var now = DateTimeOffset.UtcNow;

        var parameterPayload = JsonSerializer.Serialize(new
        {
            intercept = 4.0d,
            coefficients = new[] { 0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d },
            means = new[] { 0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d },
            scales = new[] { 1d, 1d, 1d, 1d, 1d, 1d, 1d, 1d, 1d },
            featureNames = RiskMlFeatureEngineering.FeatureNames
        });

        db.RiskMlModels.Add(new RiskMlModel
        {
            Id = Guid.NewGuid(),
            ModelKey = RiskMlFeatureEngineering.ModelKey,
            Version = 1,
            Algorithm = "logistic_regression_v1",
            HorizonDays = 30,
            FeatureSchema = "{\"features\":[]}",
            Parameters = parameterPayload,
            Metrics = "{\"accuracy\":0.80,\"auc\":0.90,\"brierScore\":0.12}",
            TrainSampleCount = 400,
            ValidationSampleCount = 100,
            PositiveRatio = 0.45m,
            IsActive = true,
            Status = "ACTIVE",
            TrainedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.GetActiveModelAsync(modelKey: null, CancellationToken.None);

        var metrics = new RiskMetrics(
            TotalOutstanding: 50_000_000m,
            OverdueAmount: 0m,
            OverdueRatio: 0m,
            MaxDaysPastDue: 0,
            LateCount: 0);
        var predicted = service.Predict(metrics, new DateOnly(2026, 2, 12));

        Assert.InRange(predicted.Probability, 0.95m, 1.00m);
        Assert.Equal("CRITICAL", predicted.Signal);
        Assert.NotEmpty(predicted.Factors);
        Assert.False(string.IsNullOrWhiteSpace(predicted.Recommendation));
    }

    private static RiskAiModelService CreateService(ConGNoDbContext db)
    {
        return new RiskAiModelService(
            db,
            new StubConnectionFactory(),
            new StubCurrentUser(),
            Options.Create(new RiskModelTrainingOptions()),
            NullLogger<RiskAiModelService>.Instance);
    }

    private static ConGNoDbContext CreateDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"risk-ai-model-service-{name}")
            .Options;
        return new ConGNoDbContext(options);
    }

    private sealed class StubCurrentUser : ICurrentUser
    {
        private static readonly Guid FixedUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public Guid? UserId => FixedUserId;
        public string? Username => "tester";
        public IReadOnlyList<string> Roles => ["Admin"];
        public string? IpAddress => "127.0.0.1";
    }

    private sealed class StubConnectionFactory : IDbConnectionFactory
    {
        public DbConnection Create() => throw new NotSupportedException("DB connection not required in this test.");
    }
}
