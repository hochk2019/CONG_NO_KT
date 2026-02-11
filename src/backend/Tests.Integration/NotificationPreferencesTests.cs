using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class NotificationPreferencesTests
{
    private readonly TestDatabaseFixture _fixture;

    public NotificationPreferencesTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Preferences_Defaults_And_Update_Persist()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var currentUser = new TestCurrentUser();
        db.Users.Add(new User
        {
            Id = currentUser.UserId!.Value,
            Username = "notify_user",
            PasswordHash = "hash",
            FullName = "Notify User",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });
        await db.SaveChangesAsync();

        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var service = new NotificationService(connectionFactory, db, currentUser);

        var defaults = await service.GetPreferencesAsync(CancellationToken.None);
        Assert.True(defaults.ReceiveNotifications);
        Assert.True(defaults.PopupEnabled);
        Assert.Contains("WARN", defaults.PopupSeverities);
        Assert.Contains("ALERT", defaults.PopupSeverities);

        var updated = await service.UpdatePreferencesAsync(
            new CongNoGolden.Application.Notifications.NotificationPreferencesUpdate(
                ReceiveNotifications: false,
                PopupEnabled: true,
                PopupSeverities: new[] { "INFO" },
                PopupSources: new[] { "RECEIPT" }),
            CancellationToken.None);

        Assert.False(updated.ReceiveNotifications);
        Assert.Contains("INFO", updated.PopupSeverities);

        var stored = await db.NotificationPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == currentUser.UserId);
        Assert.NotNull(stored);
        Assert.False(stored!.ReceiveNotifications);
    }

    [Fact]
    public async Task Unread_Count_And_Mark_All_Read()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var currentUser = new TestCurrentUser();
        db.Users.Add(new User
        {
            Id = currentUser.UserId!.Value,
            Username = "notify_user",
            PasswordHash = "hash",
            FullName = "Notify User",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = currentUser.UserId.Value,
            Title = "Thông báo 1",
            Severity = "INFO",
            Source = "SYSTEM",
            CreatedAt = DateTimeOffset.UtcNow
        });

        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = currentUser.UserId.Value,
            Title = "Thông báo 2",
            Severity = "WARN",
            Source = "RECEIPT",
            CreatedAt = DateTimeOffset.UtcNow,
            ReadAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var service = new NotificationService(connectionFactory, db, currentUser);

        var count = await service.GetUnreadCountAsync(CancellationToken.None);
        Assert.Equal(1, count.Count);

        await service.MarkAllReadAsync(CancellationToken.None);

        var unreadAfter = await service.GetUnreadCountAsync(CancellationToken.None);
        Assert.Equal(0, unreadAfter.Count);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.notifications, " +
            "congno.notification_preferences, " +
            "congno.users " +
            "RESTART IDENTITY CASCADE;");
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId => Guid.Parse("77777777-7777-7777-7777-777777777777");
        public string? Username => "notify_user";
        public IReadOnlyList<string> Roles => new[] { "Admin" };
        public string? IpAddress => "127.0.0.1";
    }
}
