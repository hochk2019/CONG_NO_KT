namespace Ops.Shared.Models;

public sealed record ServiceConfigDto(string Name, string StartMode, string ServiceAccount, string? DisplayName);

public sealed record ServiceConfigUpdateRequest(
    string Name,
    string StartMode,
    string? ServiceAccount,
    string? ServicePassword);

public sealed record ServiceRecoveryDto(
    string Name,
    string FirstFailure,
    string SecondFailure,
    string SubsequentFailure,
    int ResetPeriodMinutes);

public sealed record ServiceRecoveryUpdateRequest(
    string Name,
    string FirstFailure,
    string SecondFailure,
    string SubsequentFailure,
    int ResetPeriodMinutes);
