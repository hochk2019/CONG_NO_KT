namespace Ops.Shared.Models;

public sealed record IisBindingUpdateRequest(
    string Protocol,
    string IpAddress,
    int Port,
    string? Host,
    bool ReplaceExisting = true);
