using Ops.Agent.Services;
using Ops.Shared.Config;

namespace Ops.Tests;

public class DatabaseAdminServiceTests
{
    [Fact]
    public void BuildCreateDatabaseSql_IncludesOwnerWhenProvided()
    {
        var sql = DatabaseAdminService.BuildCreateDatabaseSql("congno_golden", "postgres");

        Assert.Contains("CREATE DATABASE", sql);
        Assert.Contains("\"congno_golden\"", sql);
        Assert.Contains("OWNER \"postgres\"", sql);
    }

    [Fact]
    public void BuildCreateDatabaseSql_OmitsOwnerWhenMissing()
    {
        var sql = DatabaseAdminService.BuildCreateDatabaseSql("congno_golden", string.Empty);

        Assert.Contains("CREATE DATABASE", sql);
        Assert.DoesNotContain("OWNER", sql);
    }

    [Fact]
    public void ResolveScriptsPath_PrioritizesBackendPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ops_test_{Guid.NewGuid():N}");
        var backend = Path.Combine(root, "backend");
        var backendScripts = Path.Combine(backend, "scripts", "db", "migrations");
        Directory.CreateDirectory(backendScripts);

        var config = OpsConfig.CreateDefault() with
        {
            Backend = OpsConfig.CreateDefault().Backend with { AppPath = backend },
            Updates = OpsConfig.CreateDefault().Updates with { RepoPath = Path.Combine(root, "repo") }
        };

        var resolved = DatabaseAdminService.ResolveScriptsPath(config);
        Assert.Equal(backendScripts, resolved);
    }

    [Fact]
    public void ResolveScriptsPath_FallsBackToRepoPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ops_test_{Guid.NewGuid():N}");
        var repo = Path.Combine(root, "repo");
        var repoScripts = Path.Combine(repo, "scripts", "db", "migrations");
        Directory.CreateDirectory(repoScripts);

        var config = OpsConfig.CreateDefault() with
        {
            Backend = OpsConfig.CreateDefault().Backend with { AppPath = Path.Combine(root, "backend") },
            Updates = OpsConfig.CreateDefault().Updates with { RepoPath = repo }
        };

        var resolved = DatabaseAdminService.ResolveScriptsPath(config);
        Assert.Equal(repoScripts, resolved);
    }

    [Fact]
    public void ResolveScriptsPath_SupportsLegacyMigrationFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ops_test_{Guid.NewGuid():N}");
        var backend = Path.Combine(root, "backend");
        var legacyScripts = Path.Combine(backend, "scripts", "db", "migration");
        Directory.CreateDirectory(legacyScripts);

        var config = OpsConfig.CreateDefault() with
        {
            Backend = OpsConfig.CreateDefault().Backend with { AppPath = backend },
            Updates = OpsConfig.CreateDefault().Updates with { RepoPath = Path.Combine(root, "repo") }
        };

        var resolved = DatabaseAdminService.ResolveScriptsPath(config);
        Assert.Equal(legacyScripts, resolved);
    }
}
