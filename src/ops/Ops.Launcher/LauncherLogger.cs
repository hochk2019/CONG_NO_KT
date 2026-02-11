using System;
using System.IO;

namespace Ops.Launcher;

public static class LauncherLogger
{
    private static readonly object Sync = new();
    private static readonly string LogPath = Path.Combine(@"C:\apps\congno\ops\logs", "launcher.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
            if (ex != null)
                line += $" | {ex.GetType().Name}: {ex.Message}";

            lock (Sync)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
        }
    }
}
