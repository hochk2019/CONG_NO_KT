namespace CongNoGolden.Application.Common.Interfaces;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Username { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> Permissions => Array.Empty<string>();
    string? IpAddress { get; }
}
