namespace Ops.Shared.Models;

public sealed record MaintenanceModeDto(bool Enabled, string? Message);

public sealed record MaintenanceModeRequest(bool Enabled, string? Message);
