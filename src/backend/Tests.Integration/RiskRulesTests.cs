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
public class RiskRulesTests
{
    private readonly TestDatabaseFixture _fixture;

    public RiskRulesTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpdateRules_Persists_And_Writes_Audit()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var currentUser = new TestCurrentUser(new[] { "Admin" });
        var audit = new AuditService(db, currentUser);
        DapperTypeHandlers.Register();
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var service = new RiskService(connectionFactory, db, currentUser, audit, new FakeRiskAiModelService());

        var request = new RiskRulesUpdateRequest(new[]
        {
            new RiskRuleUpdateItem("VERY_HIGH", 30, 0.5m, 2, true, "ALL"),
            new RiskRuleUpdateItem("HIGH", 15, 0.3m, 2, true, "ANY"),
            new RiskRuleUpdateItem("MEDIUM", 7, 0.1m, 1, true, "ALL"),
            new RiskRuleUpdateItem("LOW", 0, 0m, 0, true, "ANY"),
        });

        await service.UpdateRulesAsync(request, CancellationToken.None);

        var rules = await db.RiskRules.AsNoTracking()
            .OrderBy(r => r.SortOrder)
            .ToListAsync();

        Assert.Equal(4, rules.Count);
        Assert.Equal("VERY_HIGH", rules[0].Level);
        Assert.Equal("ALL", rules[0].MatchMode);
        Assert.Equal(30, rules[0].MinOverdueDays);
        Assert.Equal(0.5m, rules[0].MinOverdueRatio);

        var auditLog = await db.AuditLogs.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Action == "RISK_RULES_UPDATE");
        Assert.NotNull(auditLog);
    }

    [Fact]
    public async Task ListCustomers_UsesMatchModeAllToAvoidFalsePositive()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var asOf = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var sellerTaxCode = "0311223344";
        var customerTaxCode = "0311223344-001";

        db.Customers.Add(new Customer
        {
            TaxCode = customerTaxCode,
            Name = "ALL Mode Customer",
            PaymentTermsDays = 5,
            CurrentBalance = 100m,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 1
        });

        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            InvoiceNo = "INV-ALL-001",
            IssueDate = asOf.AddDays(-25),
            RevenueExclVat = 100m,
            VatAmount = 0m,
            TotalAmount = 100m,
            OutstandingAmount = 100m,
            InvoiceType = "GTGT",
            Status = "OPEN",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 1
        });

        db.RiskRules.AddRange(
            new Infrastructure.Data.Entities.RiskRule
            {
                Id = Guid.NewGuid(),
                Level = "VERY_HIGH",
                MatchMode = "ALL",
                MinOverdueDays = 999,
                MinOverdueRatio = 2m,
                MinLateCount = 99,
                IsActive = true,
                SortOrder = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Infrastructure.Data.Entities.RiskRule
            {
                Id = Guid.NewGuid(),
                Level = "HIGH",
                MatchMode = "ALL",
                MinOverdueDays = 30,
                MinOverdueRatio = 0.5m,
                MinLateCount = 2,
                IsActive = true,
                SortOrder = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Infrastructure.Data.Entities.RiskRule
            {
                Id = Guid.NewGuid(),
                Level = "MEDIUM",
                MatchMode = "ALL",
                MinOverdueDays = 999,
                MinOverdueRatio = 2m,
                MinLateCount = 99,
                IsActive = true,
                SortOrder = 3,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Infrastructure.Data.Entities.RiskRule
            {
                Id = Guid.NewGuid(),
                Level = "LOW",
                MatchMode = "ANY",
                MinOverdueDays = 0,
                MinOverdueRatio = 0,
                MinLateCount = 0,
                IsActive = true,
                SortOrder = 4,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        await db.SaveChangesAsync();

        var currentUser = new TestCurrentUser(new[] { "Admin" });
        var audit = new AuditService(db, currentUser);
        DapperTypeHandlers.Register();
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var service = new RiskService(connectionFactory, db, currentUser, audit, new FakeRiskAiModelService());

        var allModeResult = await service.ListCustomersAsync(
            new RiskCustomerListRequest(
                Search: null,
                OwnerId: null,
                Level: null,
                AsOfDate: asOf,
                Page: 1,
                PageSize: 20,
                Sort: "riskLevel",
                Order: "desc"),
            CancellationToken.None);

        var allModeCustomer = Assert.Single(allModeResult.Items);
        Assert.Equal("LOW", allModeCustomer.RiskLevel);

        var highRule = await db.RiskRules.FirstAsync(r => r.Level == "HIGH");
        highRule.MatchMode = "ANY";
        highRule.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var anyModeResult = await service.ListCustomersAsync(
            new RiskCustomerListRequest(
                Search: null,
                OwnerId: null,
                Level: null,
                AsOfDate: asOf,
                Page: 1,
                PageSize: 20,
                Sort: "riskLevel",
                Order: "desc"),
            CancellationToken.None);

        var anyModeCustomer = Assert.Single(anyModeResult.Items);
        Assert.Equal("HIGH", anyModeCustomer.RiskLevel);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.receipt_allocations, " +
            "congno.receipts, " +
            "congno.invoices, " +
            "congno.advances, " +
            "congno.customers, " +
            "congno.users, " +
            "congno.audit_logs, " +
            "congno.risk_rules " +
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
        public RiskAiPrediction Predict(RiskMetrics metrics, DateOnly asOfDate) =>
            new(0.5m, "MEDIUM", Array.Empty<RiskAiFactorContribution>(), "Theo dõi định kỳ.");

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
