using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Imports;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class ImportBatchListTests
{
    private readonly TestDatabaseFixture _fixture;

    public ImportBatchListTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListAsync_Search_Matches_File_User_And_BatchId()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "importer",
            PasswordHash = "hash",
            FullName = "Importer One",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };
        db.Users.Add(user);

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            Type = "INVOICE",
            Source = "UPLOAD",
            Status = "STAGING",
            FileName = "import_test.xlsx",
            CreatedBy = user.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ImportBatches.Add(batch);

        await db.SaveChangesAsync();

        var service = new ImportBatchService(db, new TestCurrentUser());

        var byFile = await service.ListAsync(
            new ImportBatchListRequest(null, null, "import_test.xlsx", 1, 20),
            CancellationToken.None);
        Assert.Single(byFile.Items);

        var byUser = await service.ListAsync(
            new ImportBatchListRequest(null, null, "Importer One", 1, 20),
            CancellationToken.None);
        Assert.Single(byUser.Items);

        var byBatch = await service.ListAsync(
            new ImportBatchListRequest(null, null, batch.Id.ToString()[..8], 1, 20),
            CancellationToken.None);
        Assert.Single(byBatch.Items);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.user_roles, " +
            "congno.users, " +
            "congno.import_batches " +
            "RESTART IDENTITY CASCADE;");
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;
        public string? Username => null;
        public IReadOnlyList<string> Roles => Array.Empty<string>();
        public string? IpAddress => null;
    }
}
