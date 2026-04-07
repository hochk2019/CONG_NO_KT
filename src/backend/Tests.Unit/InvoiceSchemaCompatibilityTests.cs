using System.Text.RegularExpressions;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class InvoiceSchemaCompatibilityTests
{
    [Fact]
    public void InvoiceModel_Allows_AdjustmentReduction_Length()
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        using var db = new ConGNoDbContext(options);
        var property = db.Model.FindEntityType(typeof(Invoice))?.FindProperty(nameof(Invoice.InvoiceType));

        Assert.NotNull(property);
        Assert.Equal(32, property!.GetMaxLength());
    }

    [Fact]
    public void InvoiceEntity_Defaults_Align_With_RuntimeSchema()
    {
        var invoice = new Invoice();

        Assert.Equal("NORMAL", invoice.InvoiceType);
        Assert.Equal("OPEN", invoice.Status);
    }

    [Fact]
    public void RuntimeMigration_Updates_InvoiceType_For_AdjustmentReduction()
    {
        var migrationPath = FindRepoFile("scripts", "db", "migrations", "034_invoice_adjustment_reduction.sql");
        var sql = File.ReadAllText(migrationPath);

        Assert.Contains("ALTER COLUMN invoice_type TYPE varchar(32)", sql);
        Assert.Contains("'ADJUSTMENT_REDUCTION'", sql);
        Assert.Matches(
            new Regex(@"invoice_type\s+NOT\s+IN\s+\('ADJUST','ADJUSTMENT_REDUCTION'\)", RegexOptions.IgnoreCase),
            sql);
    }

    private static string FindRepoFile(params string[] relativeSegments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(relativeSegments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException(
            $"Không tìm thấy file kiểm thử cần thiết: {Path.Combine(relativeSegments)}");
    }
}
