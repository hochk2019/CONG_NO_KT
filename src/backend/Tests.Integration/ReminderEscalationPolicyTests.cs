using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Reminders;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public sealed class ReminderEscalationPolicyTests
{
    private readonly TestDatabaseFixture _fixture;

    public ReminderEscalationPolicyTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Run_WithRepeatedAttempts_EscalatesRecipients()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var ownerId = Guid.Parse("92000000-0000-0000-0000-000000000001");
        var supervisorId = Guid.Parse("92000000-0000-0000-0000-000000000002");
        var adminId = Guid.Parse("92000000-0000-0000-0000-000000000003");

        await SeedEscalationScenarioAsync(db, ownerId, supervisorId, adminId);

        DapperTypeHandlers.Register();
        var currentUser = new TestCurrentUser(adminId, new[] { "Admin" });
        var audit = new AuditService(db, currentUser);
        var service = new ReminderService(
            new NpgsqlConnectionFactory(_fixture.ConnectionString),
            db,
            currentUser,
            audit,
            new TestZaloClient());

        await service.UpdateSettingsAsync(
            new ReminderSettingsUpdateRequest(
                Enabled: true,
                FrequencyDays: 7,
                UpcomingDueDays: 7,
                EscalationMaxAttempts: 5,
                EscalationCooldownHours: 0,
                EscalateToSupervisorAfter: 2,
                EscalateToAdminAfter: 3,
                Channels: new[] { "IN_APP" },
                TargetLevels: new[] { "VERY_HIGH" }),
            CancellationToken.None);

        await service.RunAsync(new ReminderRunRequest(Force: true), CancellationToken.None);
        await service.RunAsync(new ReminderRunRequest(Force: true), CancellationToken.None);
        await service.RunAsync(new ReminderRunRequest(Force: true), CancellationToken.None);

        var sentLogs = await db.ReminderLogs
            .AsNoTracking()
            .Where(x => x.Status == "SENT" && x.Channel == "IN_APP")
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        Assert.Equal(3, sentLogs.Count);
        Assert.Equal(1, sentLogs[0].EscalationLevel);
        Assert.Equal(2, sentLogs[1].EscalationLevel);
        Assert.Equal(3, sentLogs[2].EscalationLevel);

        var normalTitleCount = await db.Notifications.CountAsync(x =>
            x.Title == "Nhắc rủi ro công nợ");
        var supervisorTitleCount = await db.Notifications.CountAsync(x =>
            x.Title == "Nhắc rủi ro công nợ (Escalate Supervisor)");
        var adminTitleCount = await db.Notifications.CountAsync(x =>
            x.Title == "Nhắc rủi ro công nợ (Escalate Admin)");

        Assert.Equal(1, normalTitleCount);
        Assert.Equal(2, supervisorTitleCount);
        Assert.Equal(3, adminTitleCount);
    }

    [Fact]
    public async Task Run_WhenCooldownActive_LogsSkipWithoutNewNotification()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var ownerId = Guid.Parse("92000000-0000-0000-0000-000000000011");
        var adminId = Guid.Parse("92000000-0000-0000-0000-000000000012");

        await SeedEscalationScenarioAsync(db, ownerId, supervisorId: null, adminId);

        DapperTypeHandlers.Register();
        var currentUser = new TestCurrentUser(adminId, new[] { "Admin" });
        var audit = new AuditService(db, currentUser);
        var service = new ReminderService(
            new NpgsqlConnectionFactory(_fixture.ConnectionString),
            db,
            currentUser,
            audit,
            new TestZaloClient());

        await service.UpdateSettingsAsync(
            new ReminderSettingsUpdateRequest(
                Enabled: true,
                FrequencyDays: 7,
                UpcomingDueDays: 7,
                EscalationMaxAttempts: 5,
                EscalationCooldownHours: 24,
                EscalateToSupervisorAfter: 2,
                EscalateToAdminAfter: 3,
                Channels: new[] { "IN_APP" },
                TargetLevels: new[] { "VERY_HIGH" }),
            CancellationToken.None);

        var firstRun = await service.RunAsync(new ReminderRunRequest(Force: true), CancellationToken.None);
        var secondRun = await service.RunAsync(new ReminderRunRequest(Force: true), CancellationToken.None);

        Assert.Equal(1, firstRun.SentCount);
        Assert.Equal(0, secondRun.SentCount);
        Assert.True(secondRun.SkippedCount >= 1);

        var logs = await db.ReminderLogs
            .AsNoTracking()
            .Where(x => x.Channel == "IN_APP")
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, logs.Count);
        Assert.Equal("SENT", logs[0].Status);
        Assert.Equal("SKIPPED", logs[1].Status);
        Assert.Equal("COOLDOWN_ACTIVE", logs[1].ErrorDetail);
        Assert.Equal(2, logs[1].EscalationLevel);

        var notifications = await db.Notifications.AsNoTracking().CountAsync();
        Assert.Equal(1, notifications);
    }

    private async Task SeedEscalationScenarioAsync(
        ConGNoDbContext db,
        Guid ownerId,
        Guid? supervisorId,
        Guid adminId)
    {
        var now = DateTimeOffset.UtcNow;
        const int adminRoleId = 1;
        const int supervisorRoleId = 2;
        const string sellerTaxCode = "SELLER-RM";
        const string customerTaxCode = "CUST-RM-01";

        db.Roles.Add(new Role { Id = adminRoleId, Code = "Admin", Name = "Admin" });
        if (supervisorId.HasValue)
        {
            db.Roles.Add(new Role { Id = supervisorRoleId, Code = "Supervisor", Name = "Supervisor" });
        }

        db.Users.Add(new User
        {
            Id = ownerId,
            Username = "owner_rm",
            PasswordHash = "hash",
            FullName = "Owner RM",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        });

        if (supervisorId.HasValue)
        {
            db.Users.Add(new User
            {
                Id = supervisorId.Value,
                Username = "supervisor_rm",
                PasswordHash = "hash",
                FullName = "Supervisor RM",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 1
            });
        }

        db.Users.Add(new User
        {
            Id = adminId,
            Username = "admin_rm",
            PasswordHash = "hash",
            FullName = "Admin RM",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        });

        db.UserRoles.Add(new UserRole { UserId = adminId, RoleId = adminRoleId });
        if (supervisorId.HasValue)
        {
            db.UserRoles.Add(new UserRole { UserId = supervisorId.Value, RoleId = supervisorRoleId });
        }

        db.Sellers.Add(new Seller
        {
            SellerTaxCode = sellerTaxCode,
            Name = "Seller RM",
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        });

        db.Customers.Add(new Customer
        {
            TaxCode = customerTaxCode,
            Name = "Customer RM",
            AccountantOwnerId = ownerId,
            PaymentTermsDays = 0,
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
            InvoiceNo = "INV-RM-001",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-15)),
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

        db.RiskRules.Add(new RiskRule
        {
            Id = Guid.NewGuid(),
            Level = "VERY_HIGH",
            MinOverdueDays = 1,
            MinOverdueRatio = 0m,
            MinLateCount = 0,
            IsActive = true,
            SortOrder = 1,
            CreatedAt = now,
            UpdatedAt = now
        });

        await db.SaveChangesAsync();
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.notifications, " +
            "congno.reminder_logs, " +
            "congno.reminder_settings, " +
            "congno.risk_rules, " +
            "congno.receipt_allocations, " +
            "congno.receipts, " +
            "congno.invoices, " +
            "congno.advances, " +
            "congno.customers, " +
            "congno.sellers, " +
            "congno.user_roles, " +
            "congno.roles, " +
            "congno.users " +
            "RESTART IDENTITY CASCADE;");
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(Guid userId, IReadOnlyList<string> roles)
        {
            UserId = userId;
            Roles = roles;
        }

        public Guid? UserId { get; }
        public string? Username => "tester";
        public IReadOnlyList<string> Roles { get; }
        public string? IpAddress => "127.0.0.1";
    }

    private sealed class TestZaloClient : IZaloClient
    {
        public Task<ZaloSendResult> SendAsync(string userId, string message, CancellationToken ct)
        {
            return Task.FromResult(new ZaloSendResult(false, "NOT_CONFIGURED"));
        }
    }
}
