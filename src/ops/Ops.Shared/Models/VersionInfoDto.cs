using System;

namespace Ops.Shared.Models;

public sealed record ComponentVersionDto(
    string Name,
    string? Version,
    DateTimeOffset? LastWriteTime,
    string? Path);
