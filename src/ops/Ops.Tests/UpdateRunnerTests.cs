using Ops.Agent.Services;
using Ops.Shared.Config;
using System.Runtime.Versioning;

namespace Ops.Tests;

[SupportedOSPlatform("windows")]
public class UpdateRunnerTests
{
    [Fact]
    public async Task UpdateBackendAsync_CopyModeRequiresSource()
    {
        var runner = new UpdateRunner(new ProcessRunner());
        var config = OpsConfig.CreateDefault() with
        {
            Updates = new UpdateConfig { Mode = "copy" }
        };

        var result = await runner.UpdateBackendAsync(config, null, CancellationToken.None);

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task UpdateBackendAsync_PreservesAppSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "target");
        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(target);

            File.WriteAllText(Path.Combine(source, "app.dll"), "new");
            File.WriteAllText(Path.Combine(target, "appsettings.Production.json"), "keep");
            Directory.CreateDirectory(Path.Combine(target, "logs"));
            File.WriteAllText(Path.Combine(target, "logs", "old.log"), "log");

            var runner = new UpdateRunner(new ProcessRunner());
            var config = OpsConfig.CreateDefault() with
            {
                Updates = new UpdateConfig { Mode = "copy", BackendPublishPath = target }
            };

            var result = await runner.UpdateBackendAsync(config, source, CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(Path.Combine(target, "appsettings.Production.json")));
            Assert.True(Directory.Exists(Path.Combine(target, "logs")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task UpdateBackendAsync_CopiesMigrationsFromRepoWhenMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "target");
        var repo = Path.Combine(root, "repo");
        var repoMigrations = Path.Combine(repo, "scripts", "db", "migrations");
        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(repoMigrations);

            File.WriteAllText(Path.Combine(source, "app.dll"), "new");
            File.WriteAllText(Path.Combine(repoMigrations, "001_init.sql"), "select 1;");

            var runner = new UpdateRunner(new ProcessRunner());
            var config = OpsConfig.CreateDefault() with
            {
                Backend = OpsConfig.CreateDefault().Backend with { AppPath = target },
                Updates = new UpdateConfig
                {
                    Mode = "copy",
                    BackendPublishPath = target,
                    RepoPath = repo
                }
            };

            var result = await runner.UpdateBackendAsync(config, source, CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(Path.Combine(target, "scripts", "db", "migrations", "001_init.sql")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task UpdateFrontendAsync_PreservesWebConfig()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "target");
        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(target);

            File.WriteAllText(Path.Combine(source, "index.html"), "new");
            File.WriteAllText(Path.Combine(target, "web.config"), "keep");
            File.WriteAllText(Path.Combine(target, "old.txt"), "old");

            var runner = new UpdateRunner(new ProcessRunner());
            var config = OpsConfig.CreateDefault() with
            {
                Updates = new UpdateConfig { Mode = "copy", FrontendPublishPath = target }
            };

            var result = await runner.UpdateFrontendAsync(config, source, CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(Path.Combine(target, "web.config")));
            Assert.False(File.Exists(Path.Combine(target, "old.txt")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}
