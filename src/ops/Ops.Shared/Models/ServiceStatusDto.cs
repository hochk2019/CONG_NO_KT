namespace Ops.Shared.Models;

public sealed record ServiceStatusDto(
    string Name,
    string Status,
    string? Message = null);
