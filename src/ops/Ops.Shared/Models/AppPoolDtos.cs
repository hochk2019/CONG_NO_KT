namespace Ops.Shared.Models;

public sealed record AppPoolStatusDto(string Name, string Status);

public sealed record AppPoolActionRequest(string Name);
