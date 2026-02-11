using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class BackupAuditMappingTests
{
    [Fact]
    public void BackupAudit_Details_UsesJsonbColumn()
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseNpgsql("Host=localhost;Database=congno_golden;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention()
            .Options;

        using var db = new ConGNoDbContext(options);
        var entity = db.Model.FindEntityType(typeof(BackupAudit));
        var property = entity?.FindProperty(nameof(BackupAudit.Details));

        Assert.NotNull(property);
        Assert.Equal("jsonb", property!.GetColumnType());
    }
}
