namespace Ops.Shared.Models;

public sealed record IisBindingDto(
    string Protocol,
    string IpAddress,
    int Port,
    string Host,
    string BindingInformation);
