using CongNoGolden.Application.Collections;
using CongNoGolden.Application.Risk;
using CongNoGolden.Infrastructure.Services;
using Xunit;

namespace Tests.Unit;

public sealed class CollectionTaskQueueTests
{
    [Fact]
    public void EnqueueFromRisk_DeduplicatesCustomer_AndCountsCreatedOnce()
    {
        var queue = new CollectionTaskQueue();
        var now = new DateTimeOffset(2026, 2, 26, 8, 0, 0, TimeSpan.Zero);

        var customers = new[]
        {
            BuildRiskCustomer(
                customerTaxCode: "0101",
                customerName: "Cong ty A - high",
                overdueRatio: 0.95m,
                maxDaysPastDue: 120,
                predictedOverdueProbability: 0.92m,
                riskLevel: "HIGH"),
            BuildRiskCustomer(
                customerTaxCode: "0101",
                customerName: "Cong ty A - duplicate",
                overdueRatio: 0.2m,
                maxDaysPastDue: 10,
                predictedOverdueProbability: 0.35m,
                riskLevel: "LOW")
        };

        var created = queue.EnqueueFromRisk(customers, maxItems: 50, minPriorityScore: 0m, now);
        var list = queue.List(new CollectionTaskListRequest(Take: 50));

        Assert.Equal(1, created);
        Assert.Single(list);
        Assert.Equal("0101", list[0].CustomerTaxCode);
        Assert.Equal("Cong ty A - high", list[0].CustomerName);
    }

    [Fact]
    public void EnqueueFromRisk_DoesNotCreate_WhenOpenTaskAlreadyExists()
    {
        var queue = new CollectionTaskQueue();
        var now = new DateTimeOffset(2026, 2, 26, 8, 0, 0, TimeSpan.Zero);

        queue.Enqueue(
            new EnqueueCollectionTaskRequest(
                CustomerTaxCode: "0102",
                CustomerName: "Cong ty B",
                OwnerId: null,
                OwnerName: null,
                TotalOutstanding: 200_000_000m,
                OverdueAmount: 130_000_000m,
                MaxDaysPastDue: 95,
                PredictedOverdueProbability: 0.88m,
                RiskLevel: "HIGH",
                AiSignal: "HIGH",
                PriorityScore: 0.91m),
            now);

        var created = queue.EnqueueFromRisk(
            new[]
            {
                BuildRiskCustomer(
                    customerTaxCode: "0102",
                    customerName: "Cong ty B",
                    overdueRatio: 0.65m,
                    maxDaysPastDue: 88,
                    predictedOverdueProbability: 0.87m,
                    riskLevel: "HIGH")
            },
            maxItems: 20,
            minPriorityScore: 0m,
            now.AddMinutes(5));

        var list = queue.List(new CollectionTaskListRequest(Take: 50));
        Assert.Equal(0, created);
        Assert.Single(list);
    }

    [Fact]
    public void UpdateStatus_SetsAndClearsCompletedAt_AsExpected()
    {
        var queue = new CollectionTaskQueue();
        var now = new DateTimeOffset(2026, 2, 26, 8, 0, 0, TimeSpan.Zero);
        var task = queue.Enqueue(
            new EnqueueCollectionTaskRequest(
                CustomerTaxCode: "0103",
                CustomerName: "Cong ty C",
                OwnerId: null,
                OwnerName: null,
                TotalOutstanding: 100_000_000m,
                OverdueAmount: 20_000_000m,
                MaxDaysPastDue: 30,
                PredictedOverdueProbability: 0.45m,
                RiskLevel: "MEDIUM",
                AiSignal: "MEDIUM",
                PriorityScore: 0.5m),
            now);

        var done = queue.UpdateStatus(task.TaskId, CollectionTaskStatusCodes.Done, "done", now.AddMinutes(1));
        Assert.NotNull(done);
        Assert.Equal(CollectionTaskStatusCodes.Done, done!.Status);
        Assert.NotNull(done.CompletedAt);
        Assert.Equal("done", done.Note);

        var reopened = queue.UpdateStatus(task.TaskId, CollectionTaskStatusCodes.Open, null, now.AddMinutes(2));
        Assert.NotNull(reopened);
        Assert.Equal(CollectionTaskStatusCodes.Open, reopened!.Status);
        Assert.Null(reopened.CompletedAt);
        Assert.Null(reopened.Note);
    }

    [Fact]
    public void EnqueueFromRisk_PrioritizesExpectedValue_OverProbabilityOnly()
    {
        var queue = new CollectionTaskQueue();
        var now = new DateTimeOffset(2026, 2, 26, 8, 0, 0, TimeSpan.Zero);

        var customers = new[]
        {
            BuildRiskCustomer(
                customerTaxCode: "0201",
                customerName: "Probability high, amount low",
                overdueRatio: 0.85m,
                maxDaysPastDue: 85,
                predictedOverdueProbability: 0.96m,
                riskLevel: "HIGH",
                totalOutstanding: 25_000_000m,
                overdueAmount: 20_000_000m),
            BuildRiskCustomer(
                customerTaxCode: "0202",
                customerName: "Expected value high",
                overdueRatio: 0.62m,
                maxDaysPastDue: 50,
                predictedOverdueProbability: 0.71m,
                riskLevel: "MEDIUM",
                totalOutstanding: 700_000_000m,
                overdueAmount: 450_000_000m)
        };

        var created = queue.EnqueueFromRisk(customers, maxItems: 10, minPriorityScore: 0m, now);
        var list = queue.List(new CollectionTaskListRequest(Take: 10));

        Assert.Equal(2, created);
        Assert.Equal(2, list.Count);
        Assert.Equal("0202", list[0].CustomerTaxCode);
        Assert.True(list[0].PriorityScore > list[1].PriorityScore);
    }

    private static RiskCustomerItem BuildRiskCustomer(
        string customerTaxCode,
        string customerName,
        decimal overdueRatio,
        int maxDaysPastDue,
        decimal predictedOverdueProbability,
        string riskLevel,
        decimal totalOutstanding = 300_000_000m,
        decimal overdueAmount = 180_000_000m) =>
        new(
            CustomerTaxCode: customerTaxCode,
            CustomerName: customerName,
            OwnerId: null,
            OwnerName: null,
            TotalOutstanding: totalOutstanding,
            OverdueAmount: overdueAmount,
            OverdueRatio: overdueRatio,
            MaxDaysPastDue: maxDaysPastDue,
            LateCount: 5,
            RiskLevel: riskLevel,
            PredictedOverdueProbability: predictedOverdueProbability,
            AiSignal: "HIGH",
            AiFactors: [],
            AiRecommendation: "Call customer");
}
