using System.Collections.Generic;

namespace Ops.Agent.Services;

public sealed class LogReader
{
    public string ReadTail(string path, int lineCount)
    {
        var resolved = ResolveLogPath(path);
        if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
            return "Log file not found";

        if (lineCount <= 0)
            return string.Empty;

        var queue = new Queue<string>(lineCount);
        using var stream = new FileStream(resolved, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (queue.Count == lineCount)
                queue.Dequeue();
            queue.Enqueue(line);
        }

        return string.Join(Environment.NewLine, queue);
    }

    public string? ResolveLogPath(string path)
    {
        if (File.Exists(path))
            return path;

        if (Directory.Exists(path))
            return FindLatestLog(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            return FindLatestLog(directory);

        return null;
    }

    public bool IsAllowedPath(string requestedPath, string backendLogPath, string logsRoot)
    {
        if (string.Equals(requestedPath, backendLogPath, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.IsNullOrWhiteSpace(logsRoot))
            return false;

        var fullRequested = Path.GetFullPath(requestedPath);
        var fullRoot = Path.GetFullPath(logsRoot);

        return fullRequested.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindLatestLog(string directory)
        => Directory.GetFiles(directory, "*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
}
