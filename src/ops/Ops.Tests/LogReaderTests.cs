using Ops.Agent.Services;

namespace Ops.Tests;

public class LogReaderTests
{
    [Fact]
    public void ReadTail_ResolvesLatestLogInDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cng-logreader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var older = Path.Combine(root, "api20240101.log");
            var newer = Path.Combine(root, "api20250101.log");
            File.WriteAllText(older, "old-1\nold-2\nold-3");
            File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddDays(-1));
            File.WriteAllText(newer, "new-1\nnew-2\nnew-3");
            File.SetLastWriteTimeUtc(newer, DateTime.UtcNow);

            var reader = new LogReader();
            var content = reader.ReadTail(root, 2);

            Assert.Equal("new-2" + Environment.NewLine + "new-3", content);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReadTail_ResolvesLatestLogFromFileDirectoryWhenFileMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cng-logreader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var logFile = Path.Combine(root, "api20250101.log");
            File.WriteAllText(logFile, "line-1\nline-2");
            File.SetLastWriteTimeUtc(logFile, DateTime.UtcNow);

            var missingPath = Path.Combine(root, "api.log");
            var reader = new LogReader();
            var content = reader.ReadTail(missingPath, 1);

            Assert.Equal("line-2", content);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}
