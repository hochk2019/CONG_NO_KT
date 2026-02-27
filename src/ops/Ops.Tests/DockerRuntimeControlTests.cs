using Ops.Agent.Services;
using Ops.Shared.Config;

namespace Ops.Tests;

public class DockerRuntimeControlTests
{
    [Fact]
    public async Task GetServiceStatusAsync_ReturnsRunning_WhenComposeListsService()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            var composeFile = Path.Combine(tempRoot, "docker-compose.yml");
            await File.WriteAllTextAsync(composeFile, "services:{}");

            string? capturedArgs = null;
            string? capturedWorkingDir = null;

            var control = new DockerRuntimeControl((_, args, workingDir, _) =>
            {
                capturedArgs = args;
                capturedWorkingDir = workingDir;
                return Task.FromResult(new CommandResult(0, "api\n", string.Empty));
            });

            var config = OpsConfig.CreateDefault() with
            {
                Runtime = new RuntimeConfig
                {
                    Mode = "docker",
                    Docker = new DockerRuntimeConfig
                    {
                        ComposeFilePath = composeFile,
                        WorkingDirectory = tempRoot,
                        ProjectName = "congno",
                        BackendService = "api",
                        FrontendService = "web"
                    }
                }
            };

            var status = await control.GetServiceStatusAsync(config, "api", CancellationToken.None);

            Assert.Equal("running", status.Status);
            Assert.Contains("compose -f", capturedArgs);
            Assert.Contains("-p \"congno\"", capturedArgs);
            Assert.Contains("ps --services --status running api", capturedArgs);
            Assert.Equal(tempRoot, capturedWorkingDir);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public async Task StartServiceAsync_ReturnsError_WhenComposeFileMissing()
    {
        var control = new DockerRuntimeControl((_, _, _, _) =>
            Task.FromResult(new CommandResult(0, string.Empty, string.Empty)));

        var config = OpsConfig.CreateDefault() with
        {
            Runtime = new RuntimeConfig
            {
                Mode = "docker",
                Docker = new DockerRuntimeConfig
                {
                    ComposeFilePath = @"C:\does-not-exist\docker-compose.yml",
                    WorkingDirectory = @"C:\does-not-exist",
                    ProjectName = "congno",
                    BackendService = "api",
                    FrontendService = "web"
                }
            }
        };

        var status = await control.StartServiceAsync(config, "api", CancellationToken.None);

        Assert.Equal("error", status.Status);
        Assert.Contains("not found", status.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
