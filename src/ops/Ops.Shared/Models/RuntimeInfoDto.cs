namespace Ops.Shared.Models;

public sealed record RuntimeInfoDto(
    string Mode,
    string? ComposeFilePath,
    string? WorkingDirectory,
    string? ProjectName,
    string? BackendService,
    string? FrontendService);
