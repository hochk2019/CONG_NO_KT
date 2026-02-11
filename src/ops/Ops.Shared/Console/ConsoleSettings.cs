using System;
using System.Collections.Generic;

namespace Ops.Shared.Console;

public sealed record ConsoleProfile
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "Server 1";
    public string BaseUrl { get; init; } = "http://localhost:6090";
    public string ApiKey { get; init; } = string.Empty;
    public DateTimeOffset? LastUsedAt { get; init; }
}

public sealed record ConsoleSettings
{
    public string ActiveProfileId { get; init; } = string.Empty;
    public List<ConsoleProfile> Profiles { get; init; } = new() { new ConsoleProfile() };
    public int AutoRefreshSeconds { get; init; } = 10;
    public bool AdvancedModeEnabled { get; init; }

    // Legacy fields (v0) - keep for migration
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;

    public static ConsoleSettings Default => new();
}
