namespace Ops.Shared.Models;

public sealed record BackendLogLevelDto(string DefaultLevel, string? SerilogLevel);

public sealed record BackendLogLevelUpdateRequest(string DefaultLevel);
