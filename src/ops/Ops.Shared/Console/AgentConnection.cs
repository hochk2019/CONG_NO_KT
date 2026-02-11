namespace Ops.Shared.Console;

public static class AgentConnection
{
    public static string NormalizeBaseUrl(string baseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(baseUrl)
            ? "http://localhost:6090"
            : baseUrl.Trim();

        if (!normalized.Contains("://", StringComparison.Ordinal))
            normalized = "http://" + normalized;

        if (!normalized.EndsWith("/", StringComparison.Ordinal))
            normalized += "/";

        return normalized;
    }
}
