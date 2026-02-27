using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Dashboard;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public sealed class DashboardPreferencesTests
{
    private readonly TestDatabaseFixture _fixture;

    public DashboardPreferencesTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Dashboard_Preferences_Defaults_And_Update_Persist()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var userId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        db.Users.Add(new User
        {
            Id = userId,
            Username = "dashboard_user",
            PasswordHash = "hash",
            FullName = "Dashboard User",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });
        await db.SaveChangesAsync();

        DapperTypeHandlers.Register();
        var service = new DashboardService(
            new NpgsqlConnectionFactory(_fixture.ConnectionString),
            new TestCurrentUser());

        var defaults = await service.GetPreferencesAsync(userId, CancellationToken.None);
        Assert.Equal(
            new[] { "executiveSummary", "kpis", "cashflow", "panels", "quickActions" },
            defaults.WidgetOrder);
        Assert.Empty(defaults.HiddenWidgets);

        var updated = await service.UpdatePreferencesAsync(
            userId,
            new UpdateDashboardPreferencesRequest(
                WidgetOrder: new[] { "quickActions", "kpis" },
                HiddenWidgets: new[] { "panels", "executiveSummary" }),
            CancellationToken.None);

        Assert.Equal("quickActions", updated.WidgetOrder[0]);
        Assert.Contains("executiveSummary", updated.HiddenWidgets);
        Assert.Contains("panels", updated.HiddenWidgets);

        var reloaded = await service.GetPreferencesAsync(userId, CancellationToken.None);
        Assert.Equal(updated.WidgetOrder, reloaded.WidgetOrder);
        Assert.Equal(updated.HiddenWidgets, reloaded.HiddenWidgets);
    }

    [Fact]
    public async Task Dashboard_Preferences_Normalize_Unknown_And_Duplicate_Items()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var userId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        db.Users.Add(new User
        {
            Id = userId,
            Username = "dashboard_user2",
            PasswordHash = "hash",
            FullName = "Dashboard User 2",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });
        await db.SaveChangesAsync();

        DapperTypeHandlers.Register();
        var service = new DashboardService(
            new NpgsqlConnectionFactory(_fixture.ConnectionString),
            new TestCurrentUser());

        var updated = await service.UpdatePreferencesAsync(
            userId,
            new UpdateDashboardPreferencesRequest(
                WidgetOrder: new[] { "quickActions", "quickActions", "unknownWidget" },
                HiddenWidgets: new[] { "kpis", "kpis", "notExists" }),
            CancellationToken.None);

        Assert.Equal("quickActions", updated.WidgetOrder[0]);
        Assert.Contains("kpis", updated.HiddenWidgets);
        Assert.Single(updated.HiddenWidgets);
        Assert.Contains("executiveSummary", updated.WidgetOrder);
        Assert.Contains("panels", updated.WidgetOrder);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE SCHEMA IF NOT EXISTS congno;
            CREATE TABLE IF NOT EXISTS congno.user_report_preferences (
                user_id uuid NOT NULL,
                report_key text NOT NULL DEFAULT 'reports',
                preferences jsonb NOT NULL DEFAULT '{{}}'::jsonb,
                created_at timestamptz NOT NULL DEFAULT now(),
                updated_at timestamptz NOT NULL DEFAULT now(),
                CONSTRAINT pk_user_report_preferences PRIMARY KEY (user_id, report_key),
                CONSTRAINT fk_user_report_preferences_user FOREIGN KEY (user_id)
                    REFERENCES congno.users(id) ON DELETE CASCADE
            );
            TRUNCATE TABLE congno.user_report_preferences, congno.users RESTART IDENTITY CASCADE;
            """);
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string? Username => "test";
        public IReadOnlyList<string> Roles => new[] { "Admin" };
        public string? IpAddress => "127.0.0.1";
    }
}
