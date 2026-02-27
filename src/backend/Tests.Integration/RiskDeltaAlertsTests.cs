using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Risk;
using CongNoGolden.Domain.Risk;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class RiskDeltaAlertsTests
{
    private readonly TestDatabaseFixture _fixture;

    public RiskDeltaAlertsTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CaptureRiskSnapshots_CreatesDeltaAlert_AndNotification()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var ownerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        const string sellerTaxCode = "0100000001";
        const string customerTaxCode = "0100000001-001";
        var now = DateTimeOffset.UtcNow;

        db.Users.Add(new User
        {
            Id = ownerId,
            Username = "owner_a",
            PasswordHash = "hash",
            FullName = "Owner A",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        });

        db.Sellers.Add(new Seller
        {
            SellerTaxCode = sellerTaxCode,
            Name = "Cong ty ban",
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        });

        db.Customers.Add(new Customer
        {
            TaxCode = customerTaxCode,
            Name = "Khach hang A",
            AccountantOwnerId = ownerId,
            PaymentTermsDays = 30,
            CurrentBalance = 100m,
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        });

        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            InvoiceNo = "INV-DELTA-001",
            IssueDate = new DateOnly(2025, 12, 31),
            RevenueExclVat = 100m,
            VatAmount = 0m,
            TotalAmount = 100m,
            OutstandingAmount = 100m,
            InvoiceType = "GTGT",
            Status = "OPEN",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        });

        await db.SaveChangesAsync();

        DapperTypeHandlers.Register();
        var currentUser = new TestCurrentUser(new[] { "Admin" });
        var audit = new AuditService(db, currentUser);
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var service = new RiskService(connectionFactory, db, currentUser, audit, new FakeRiskAiModelService());

        var firstRun = await service.CaptureRiskSnapshotsAsync(
            new DateOnly(2026, 1, 15),
            absoluteThreshold: 0.15m,
            relativeThresholdRatio: 0.25m,
            CancellationToken.None);

        var secondRun = await service.CaptureRiskSnapshotsAsync(
            new DateOnly(2026, 3, 15),
            absoluteThreshold: 0.15m,
            relativeThresholdRatio: 0.25m,
            CancellationToken.None);

        Assert.Equal(1, firstRun.SnapshotCount);
        Assert.Equal(0, firstRun.AlertCount);
        Assert.Equal(1, secondRun.SnapshotCount);
        Assert.Equal(1, secondRun.AlertCount);
        Assert.Equal(1, secondRun.NotificationCount);

        var alert = await db.RiskDeltaAlerts.AsNoTracking().SingleAsync();
        Assert.Equal(customerTaxCode, alert.CustomerTaxCode);
        Assert.Equal(new DateOnly(2026, 3, 15), alert.AsOfDate);
        Assert.Equal("OPEN", alert.Status);
        Assert.True(Math.Abs(alert.Delta) >= 0.15m);

        var notification = await db.Notifications.AsNoTracking().SingleAsync();
        Assert.Equal(ownerId, notification.UserId);
        Assert.Equal("RISK_DELTA", notification.Source);
        Assert.Equal("WARN", notification.Severity);
    }

    [Fact]
    public async Task DeltaAlerts_AndScoreHistory_CanBeQueried()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var ownerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        const string sellerTaxCode = "0100000002";
        const string customerTaxCode = "0100000002-001";
        var now = DateTimeOffset.UtcNow;

        db.Users.Add(new User
        {
            Id = ownerId,
            Username = "owner_b",
            PasswordHash = "hash",
            FullName = "Owner B",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        });

        db.Sellers.Add(new Seller
        {
            SellerTaxCode = sellerTaxCode,
            Name = "Cong ty ban B",
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        });

        db.Customers.Add(new Customer
        {
            TaxCode = customerTaxCode,
            Name = "Khach hang B",
            AccountantOwnerId = ownerId,
            PaymentTermsDays = 10,
            CurrentBalance = 100m,
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        });

        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            InvoiceNo = "INV-DELTA-002",
            IssueDate = new DateOnly(2026, 1, 1),
            RevenueExclVat = 100m,
            VatAmount = 0m,
            TotalAmount = 100m,
            OutstandingAmount = 100m,
            InvoiceType = "GTGT",
            Status = "OPEN",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        });

        await db.SaveChangesAsync();

        DapperTypeHandlers.Register();
        var currentUser = new TestCurrentUser(new[] { "Admin" });
        var audit = new AuditService(db, currentUser);
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var service = new RiskService(connectionFactory, db, currentUser, audit, new FakeRiskAiModelService());

        await service.CaptureRiskSnapshotsAsync(
            new DateOnly(2026, 1, 5),
            absoluteThreshold: 0.10m,
            relativeThresholdRatio: 0.20m,
            CancellationToken.None);
        await service.CaptureRiskSnapshotsAsync(
            new DateOnly(2026, 2, 10),
            absoluteThreshold: 0.10m,
            relativeThresholdRatio: 0.20m,
            CancellationToken.None);

        var alerts = await service.ListDeltaAlertsAsync(
            new RiskDeltaAlertListRequest(
                Status: "OPEN",
                CustomerTaxCode: customerTaxCode,
                FromDate: null,
                ToDate: null,
                Page: 1,
                PageSize: 20),
            CancellationToken.None);

        var history = await service.GetScoreHistoryAsync(
            customerTaxCode,
            fromDate: null,
            toDate: null,
            take: 30,
            CancellationToken.None);

        Assert.Single(alerts.Items);
        Assert.Equal(2, history.Count);
        Assert.True(history[1].Score >= history[0].Score);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.notifications, " +
            "congno.risk_delta_alerts, " +
            "congno.risk_score_snapshots, " +
            "congno.receipt_allocations, " +
            "congno.receipts, " +
            "congno.invoices, " +
            "congno.advances, " +
            "congno.risk_rules, " +
            "congno.customers, " +
            "congno.sellers, " +
            "congno.users, " +
            "congno.audit_logs " +
            "RESTART IDENTITY CASCADE;");
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(IReadOnlyList<string> roles)
        {
            Roles = roles;
        }

        public Guid? UserId => Guid.Parse("33333333-3333-3333-3333-333333333333");
        public string? Username => "tester";
        public IReadOnlyList<string> Roles { get; }
        public string? IpAddress => "127.0.0.1";
    }

    private sealed class FakeRiskAiModelService : IRiskAiModelService
    {
        public RiskAiPrediction Predict(RiskMetrics metrics, DateOnly asOfDate) => RiskAiScorer.Predict(metrics);

        public Task<RiskMlTrainResult> TrainAsync(RiskMlTrainRequest request, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<RiskMlModelSummary>> ListModelsAsync(string? modelKey, int take, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<RiskMlTrainingRunSummary>> ListTrainingRunsAsync(string? modelKey, int take, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<RiskMlModelSummary?> GetActiveModelAsync(string? modelKey, CancellationToken ct)
            => Task.FromResult<RiskMlModelSummary?>(null);

        public Task<RiskMlModelSummary> ActivateModelAsync(Guid modelId, CancellationToken ct)
            => throw new NotSupportedException();
    }
}
