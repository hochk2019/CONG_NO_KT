using System.Data;
using Dapper;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Reminders;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class ReminderRunTests
{
    private readonly TestDatabaseFixture _fixture;

    public ReminderRunTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Run_Reminders_Writes_Log_And_Notifications()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var ownerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        db.Users.Add(new User
        {
            Id = ownerId,
            Username = "owner",
            PasswordHash = "hash",
            FullName = "Owner",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        db.Sellers.Add(new Seller
        {
            SellerTaxCode = "SELLER01",
            Name = "Seller 01",
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        db.Customers.Add(new Customer
        {
            TaxCode = "CUST01",
            Name = "Customer 01",
            AccountantOwnerId = ownerId,
            PaymentTermsDays = 0,
            Status = "ACTIVE",
            CurrentBalance = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = "SELLER01",
            CustomerTaxCode = "CUST01",
            InvoiceNo = "INV001",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            TotalAmount = 100m,
            OutstandingAmount = 100m,
            Status = "OPEN",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        db.RiskRules.Add(new RiskRule
        {
            Id = Guid.NewGuid(),
            Level = "VERY_HIGH",
            MinOverdueDays = 1,
            MinOverdueRatio = 0,
            MinLateCount = 0,
            IsActive = true,
            SortOrder = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        var currentUser = new TestCurrentUser(new[] { "Admin" });
        var audit = new AuditService(db, currentUser);
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var service = new ReminderService(connectionFactory, db, currentUser, audit, new TestZaloClient());

        await service.UpdateSettingsAsync(new ReminderSettingsUpdateRequest(
            true,
            7,
            7,
            new[] { "IN_APP" },
            new[] { "VERY_HIGH" }), CancellationToken.None);

        var result = await service.RunAsync(true, CancellationToken.None);

        Assert.True(result.TotalCandidates > 0);

        var logs = await db.ReminderLogs.AsNoTracking().ToListAsync();
        Assert.NotEmpty(logs);

        var notifications = await db.Notifications.AsNoTracking().ToListAsync();
        Assert.NotEmpty(notifications);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.notifications, " +
            "congno.reminder_logs, " +
            "congno.reminder_settings, " +
            "congno.risk_rules, " +
            "congno.invoices, " +
            "congno.customers, " +
            "congno.sellers, " +
            "congno.users " +
            "RESTART IDENTITY CASCADE;");
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(IReadOnlyList<string> roles)
        {
            Roles = roles;
        }

        public Guid? UserId => Guid.Parse("55555555-5555-5555-5555-555555555555");
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

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        }

        public override DateOnly Parse(object value)
        {
            return value switch
            {
                DateTime dateTime => DateOnly.FromDateTime(dateTime),
                DateOnly dateOnly => dateOnly,
                _ => DateOnly.FromDateTime(Convert.ToDateTime(value))
            };
        }
    }
}
