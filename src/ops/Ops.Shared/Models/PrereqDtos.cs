namespace Ops.Shared.Models;

public sealed record PrereqItemDto(
    string Id,
    string Name,
    string Description,
    bool IsInstalled,
    string? Version,
    string DownloadUrl,
    bool RequiresRestart,
    string? Notes);

public sealed record PrereqInstallRequest(string Id);
