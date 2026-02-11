using System.IO.Compression;
using System.Collections.Generic;
using Ops.Shared.Config;

namespace Ops.Agent.Services;

public sealed class UpdateRunner(ProcessRunner runner)
{
    private static readonly string[] BackendPreservePatterns =
    {
        "appsettings*.json",
        "logs"
    };

    private static readonly string[] FrontendPreservePatterns =
    {
        "web.config"
    };

    public async Task<CommandResult> UpdateBackendAsync(OpsConfig config, string? sourcePath, CancellationToken ct)
    {
        CommandResult result;
        if (string.Equals(config.Updates.Mode, "git", StringComparison.OrdinalIgnoreCase))
        {
            var repo = config.Updates.RepoPath;
            var pull = await runner.RunAsync("git", "pull", repo, ct);
            if (pull.ExitCode != 0)
                return pull;

            var apiProj = Path.Combine(repo, "src", "backend", "Api", "CongNoGolden.Api.csproj");
            var args = $"publish \"{apiProj}\" -c Release -o \"{config.Updates.BackendPublishPath}\"";
            result = await runner.RunAsync("dotnet", args, repo, ct);
            if (result.ExitCode != 0)
                return result;

            return MergeResults(result, EnsureMigrations(config, repo));
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
            return new CommandResult(1, string.Empty, "SourcePath required for copy mode");

        result = CopyOrExtract(sourcePath, config.Updates.BackendPublishPath, BackendPreservePatterns);
        if (result.ExitCode != 0)
            return result;

        return MergeResults(result, EnsureMigrations(config, sourcePath));
    }

    public async Task<CommandResult> UpdateFrontendAsync(OpsConfig config, string? sourcePath, CancellationToken ct)
    {
        if (string.Equals(config.Updates.Mode, "git", StringComparison.OrdinalIgnoreCase))
        {
            var repo = config.Updates.RepoPath;
            var pull = await runner.RunAsync("git", "pull", repo, ct);
            if (pull.ExitCode != 0)
                return pull;

            var frontendDir = Path.Combine(repo, "src", "frontend");
            var npmInstall = await runner.RunAsync("npm", "ci", frontendDir, ct);
            if (npmInstall.ExitCode != 0)
                return npmInstall;

            var build = await runner.RunAsync("npm", "run build", frontendDir, ct);
            if (build.ExitCode != 0)
                return build;

            var dist = Path.Combine(frontendDir, "dist");
            return CopyOrExtract(dist, config.Updates.FrontendPublishPath, FrontendPreservePatterns);
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
            return new CommandResult(1, string.Empty, "SourcePath required for copy mode");

        return CopyOrExtract(sourcePath, config.Updates.FrontendPublishPath, FrontendPreservePatterns);
    }

    private static CommandResult CopyOrExtract(string sourcePath, string targetPath, IReadOnlyCollection<string> preservePatterns)
    {
        try
        {
            var preserveRoot = string.Empty;
            if (Directory.Exists(targetPath))
                preserveRoot = PreserveEntries(targetPath, preservePatterns);

            if (File.Exists(sourcePath) && sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(targetPath))
                    Directory.Delete(targetPath, true);

                Directory.CreateDirectory(targetPath);
                ZipFile.ExtractToDirectory(sourcePath, targetPath);
                RestoreEntries(preserveRoot, targetPath);
                return new CommandResult(0, "Extracted zip", string.Empty);
            }

            if (!Directory.Exists(sourcePath))
                return new CommandResult(1, string.Empty, "Source path not found");

            if (Directory.Exists(targetPath))
                Directory.Delete(targetPath, true);

            CopyDirectory(sourcePath, targetPath);
            RestoreEntries(preserveRoot, targetPath);
            return new CommandResult(0, "Copied directory", string.Empty);
        }
        catch (Exception ex)
        {
            return new CommandResult(1, string.Empty, ex.Message);
        }
    }

    private static string PreserveEntries(string targetPath, IReadOnlyCollection<string> preservePatterns)
    {
        if (preservePatterns.Count == 0)
            return string.Empty;

        var preserveRoot = Path.Combine(Path.GetTempPath(), $"cng_ops_preserve_{Guid.NewGuid():N}");
        Directory.CreateDirectory(preserveRoot);

        foreach (var entry in CollectPreserveEntries(targetPath, preservePatterns))
        {
            var relative = Path.GetRelativePath(targetPath, entry);
            var destination = Path.Combine(preserveRoot, relative);
            var destinationDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(destinationDir))
                Directory.CreateDirectory(destinationDir);

            if (Directory.Exists(entry))
            {
                if (Directory.Exists(destination))
                    Directory.Delete(destination, true);
                CopyDirectory(entry, destination);
                Directory.Delete(entry, true);
            }
            else if (File.Exists(entry))
            {
                File.Copy(entry, destination, true);
                File.Delete(entry);
            }
        }

        return preserveRoot;
    }

    private static void RestoreEntries(string preserveRoot, string targetPath)
    {
        if (string.IsNullOrWhiteSpace(preserveRoot) || !Directory.Exists(preserveRoot))
            return;

        foreach (var directory in Directory.GetDirectories(preserveRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(preserveRoot, directory);
            Directory.CreateDirectory(Path.Combine(targetPath, relative));
        }

        foreach (var file in Directory.GetFiles(preserveRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(preserveRoot, file);
            var destination = Path.Combine(targetPath, relative);
            var destinationDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(destinationDir))
                Directory.CreateDirectory(destinationDir);
            File.Copy(file, destination, true);
        }

        Directory.Delete(preserveRoot, true);
    }

    private static List<string> CollectPreserveEntries(string targetPath, IReadOnlyCollection<string> preservePatterns)
    {
        var entries = new List<string>();
        foreach (var pattern in preservePatterns)
        {
            if (pattern.Contains('*') || pattern.Contains('?'))
            {
                entries.AddRange(Directory.GetFiles(targetPath, pattern, SearchOption.TopDirectoryOnly));
                entries.AddRange(Directory.GetDirectories(targetPath, pattern, SearchOption.TopDirectoryOnly));
                continue;
            }

            var candidate = Path.Combine(targetPath, pattern);
            if (File.Exists(candidate) || Directory.Exists(candidate))
                entries.Add(candidate);
        }

        return entries;
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var target = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, target, true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            CopyDirectory(dir, Path.Combine(targetDir, name));
        }
    }

    private static CommandResult EnsureMigrations(OpsConfig config, string? sourceRoot)
    {
        var backendRoot = config.Backend.AppPath;
        if (string.IsNullOrWhiteSpace(backendRoot))
            return new CommandResult(0, string.Empty, "Backend.AppPath trống, bỏ qua copy migrations");

        var targetMigrations = Path.Combine(backendRoot, "scripts", "db", "migrations");
        var targetMigration = Path.Combine(backendRoot, "scripts", "db", "migration");
        if (Directory.Exists(targetMigrations) || Directory.Exists(targetMigration))
            return new CommandResult(0, "Migrations đã tồn tại", string.Empty);

        var source = ResolveMigrationsPath(sourceRoot)
                     ?? ResolveMigrationsPath(config.Updates.RepoPath);

        if (string.IsNullOrWhiteSpace(source))
            return new CommandResult(
                0,
                string.Empty,
                $"Không tìm thấy scripts\\db\\migrations để copy. Vui lòng copy thủ công vào {targetMigrations}");

        try
        {
            CopyDirectory(source, targetMigrations);
            return new CommandResult(0, $"Đã copy migrations vào {targetMigrations}", string.Empty);
        }
        catch (Exception ex)
        {
            return new CommandResult(1, string.Empty, ex.Message);
        }
    }

    private static string? ResolveMigrationsPath(string? root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return null;

        var migrations = Path.Combine(root, "scripts", "db", "migrations");
        if (Directory.Exists(migrations))
            return migrations;

        var migration = Path.Combine(root, "scripts", "db", "migration");
        if (Directory.Exists(migration))
            return migration;

        return null;
    }

    private static CommandResult MergeResults(CommandResult primary, CommandResult secondary)
    {
        if (secondary.ExitCode != 0)
        {
            return new CommandResult(
                secondary.ExitCode,
                CombineMessage(primary.Stdout, secondary.Stdout),
                CombineMessage(primary.Stderr, secondary.Stderr));
        }

        return new CommandResult(
            primary.ExitCode,
            CombineMessage(primary.Stdout, secondary.Stdout),
            CombineMessage(primary.Stderr, secondary.Stderr));
    }

    private static string CombineMessage(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
            return second ?? string.Empty;
        if (string.IsNullOrWhiteSpace(second))
            return first;
        return $"{first.TrimEnd()}{Environment.NewLine}{second.Trim()}";
    }
}
