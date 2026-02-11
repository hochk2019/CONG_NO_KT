using CongNoGolden.Application.Reports;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class ReportPreferencesTests
{
    private readonly TestDatabaseFixture _fixture;

    public ReportPreferencesTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Report_Preferences_Defaults_And_Update_Persist()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var userId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        db.Users.Add(new User
        {
            Id = userId,
            Username = "report_user",
            PasswordHash = "hash",
            FullName = "Report User",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });
        await db.SaveChangesAsync();

        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var service = new ReportService(connectionFactory);

        var defaults = await service.GetPreferencesAsync(userId, CancellationToken.None);
        Assert.Equal(7, defaults.DueSoonDays);
        Assert.Contains("totalOutstanding", defaults.KpiOrder);

        var updated = await service.UpdatePreferencesAsync(
            userId,
            new UpdateReportPreferencesRequest(
                KpiOrder: new[] { "onTimeCustomers", "overdueAmount" },
                DueSoonDays: 10),
            CancellationToken.None);

        Assert.Equal(10, updated.DueSoonDays);
        Assert.Equal("onTimeCustomers", updated.KpiOrder[0]);

        var reloaded = await service.GetPreferencesAsync(userId, CancellationToken.None);
        Assert.Equal(updated.DueSoonDays, reloaded.DueSoonDays);
        Assert.Equal(updated.KpiOrder, reloaded.KpiOrder);
    }

    [Fact]
    public async Task Report_Preferences_Clamp_DueSoonDays()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var userId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        db.Users.Add(new User
        {
            Id = userId,
            Username = "report_user2",
            PasswordHash = "hash",
            FullName = "Report User 2",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });
        await db.SaveChangesAsync();

        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var service = new ReportService(connectionFactory);

        var updated = await service.UpdatePreferencesAsync(
            userId,
            new UpdateReportPreferencesRequest(
                KpiOrder: new[] { "dueSoonAmount" },
                DueSoonDays: 120),
            CancellationToken.None);

        Assert.Equal(10, updated.DueSoonDays);
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

}
