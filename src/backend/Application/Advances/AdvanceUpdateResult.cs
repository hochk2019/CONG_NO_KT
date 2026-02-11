namespace CongNoGolden.Application.Advances;

public sealed record AdvanceUpdateResult(Guid Id, int Version, string? Description);
