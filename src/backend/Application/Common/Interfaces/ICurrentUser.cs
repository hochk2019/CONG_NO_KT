namespace CongNoGolden.Application.Common.Interfaces;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Username { get; }
    IReadOnlyList<string> Roles { get; }
    string? IpAddress { get; }
}
