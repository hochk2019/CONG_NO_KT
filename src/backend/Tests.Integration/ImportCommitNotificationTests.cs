using System.Text.Json;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Imports;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public sealed class ImportCommitNotificationTests
{
    private readonly TestDatabaseFixture _fixture;

    public ImportCommitNotificationTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CommitAsync_Creates_Import_Notification()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var now = DateTimeOffset.UtcNow;
        var userId = Guid.Parse("88888888-8888-8888-8888-888888888888");

        db.Users.Add(new User
        {
            Id = userId,
            Username = "import_user",
            PasswordHash = "hash",
            FullName = "Import User",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        });

        db.Sellers.Add(new Seller
        {
            SellerTaxCode = "SELLER01",
            Name = "Seller 01",
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        });

        var batchId = Guid.NewGuid();
        db.ImportBatches.Add(new ImportBatch
        {
            Id = batchId,
            Type = "ADVANCE",
            Source = "FILE",
            Status = "STAGING",
            CreatedBy = userId,
            CreatedAt = now
        });

        var raw = JsonSerializer.Serialize(new
        {
            seller_tax_code = "SELLER01",
            customer_tax_code = "CUST01",
            amount = 100000,
            advance_date = "2026-02-01",
            advance_no = "ADV-001",
            description = "Import test"
        });

        db.ImportStagingRows.Add(new ImportStagingRow
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            RowNo = 1,
            RawData = raw,
            ValidationStatus = ImportStagingHelpers.StatusOk,
            ActionSuggestion = "INSERT",
            CreatedAt = now
        });

        await db.SaveChangesAsync();

        var currentUser = new TestCurrentUser(userId);
        var audit = new AuditService(db, currentUser);
        var service = new ImportCommitService(db, currentUser, audit);

        await service.CommitAsync(batchId, new ImportCommitRequest(null), CancellationToken.None);

        var notification = await db.Notifications.AsNoTracking()
            .FirstOrDefaultAsync(n => n.UserId == userId);

        Assert.NotNull(notification);
        Assert.Equal("IMPORT", notification!.Source);
        Assert.Equal("INFO", notification.Severity);
        Assert.Contains(batchId.ToString(), notification.Metadata ?? string.Empty);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.notifications, " +
            "congno.notification_preferences, " +
            "congno.import_staging_rows, " +
            "congno.import_batches, " +
            "congno.advances, " +
            "congno.customers, " +
            "congno.sellers, " +
            "congno.users " +
            "RESTART IDENTITY CASCADE;");
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(Guid userId)
        {
            UserId = userId;
        }

        public Guid? UserId { get; }
        public string? Username => "import_user";
        public IReadOnlyList<string> Roles => new[] { "Admin" };
        public string? IpAddress => "127.0.0.1";
    }
}
