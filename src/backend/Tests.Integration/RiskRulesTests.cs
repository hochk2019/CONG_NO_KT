using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Risk;
using CongNoGolden.Infrastructure.Data;
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
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var service = new RiskService(connectionFactory, db, currentUser, audit);

        var request = new RiskRulesUpdateRequest(new[]
        {
            new RiskRuleUpdateItem("VERY_HIGH", 30, 0.5m, 2, true),
            new RiskRuleUpdateItem("HIGH", 15, 0.3m, 2, true),
            new RiskRuleUpdateItem("MEDIUM", 7, 0.1m, 1, true),
            new RiskRuleUpdateItem("LOW", 0, 0m, 0, true),
        });

        await service.UpdateRulesAsync(request, CancellationToken.None);

        var rules = await db.RiskRules.AsNoTracking()
            .OrderBy(r => r.SortOrder)
            .ToListAsync();

        Assert.Equal(4, rules.Count);
        Assert.Equal("VERY_HIGH", rules[0].Level);
        Assert.Equal(30, rules[0].MinOverdueDays);
        Assert.Equal(0.5m, rules[0].MinOverdueRatio);

        var auditLog = await db.AuditLogs.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Action == "RISK_RULES_UPDATE");
        Assert.NotNull(auditLog);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
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
}
