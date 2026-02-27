using CongNoGolden.Infrastructure.Services.Common;
using CongNoGolden.Application.Common.Interfaces;
using Xunit;

namespace Tests.Unit;

public sealed class CurrentUserAccessTests
{
    [Fact]
    public void EnsureUser_Throws_WhenUserMissing()
    {
        var user = new TestCurrentUser(null, Array.Empty<string>());

        Assert.Throws<UnauthorizedAccessException>(() => user.EnsureUser());
    }

    [Fact]
    public void EnsureUser_ReturnsUserId_WhenPresent()
    {
        var id = Guid.NewGuid();
        var user = new TestCurrentUser(id, Array.Empty<string>());

        var actual = user.EnsureUser();

        Assert.Equal(id, actual);
    }

    [Fact]
    public void ResolveOwnerFilter_ReturnsExplicitOwner_ForPrivilegedRole()
    {
        var explicitOwner = Guid.NewGuid();
        var user = new TestCurrentUser(Guid.NewGuid(), new[] { "Supervisor" });

        var actual = user.ResolveOwnerFilter(explicitOwner);

        Assert.Equal(explicitOwner, actual);
    }

    [Fact]
    public void ResolveOwnerFilter_ReturnsCurrentUser_ForNonPrivilegedRole()
    {
        var id = Guid.NewGuid();
        var user = new TestCurrentUser(id, new[] { "Accountant" });

        var actual = user.ResolveOwnerFilter(Guid.NewGuid());

        Assert.Equal(id, actual);
    }

    [Fact]
    public void ResolveOwnerFilter_Throws_WhenNonPrivilegedUserMissing()
    {
        var user = new TestCurrentUser(null, new[] { "Accountant" });

        Assert.Throws<UnauthorizedAccessException>(() => user.ResolveOwnerFilter(Guid.NewGuid()));
    }

    [Fact]
    public void ResolveOwnerFilter_HonorsCustomPrivilegedRoles()
    {
        var id = Guid.NewGuid();
        var user = new TestCurrentUser(id, new[] { "Viewer" });

        var actual = user.ResolveOwnerFilter(
            privilegedRoles: new[] { "Admin", "Supervisor" });

        Assert.Equal(id, actual);
    }

    [Fact]
    public void HasAnyRole_IsCaseInsensitive()
    {
        var user = new TestCurrentUser(Guid.NewGuid(), new[] { "admin" });

        var actual = user.HasAnyRole("Admin");

        Assert.True(actual);
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(Guid? userId, IReadOnlyList<string> roles)
        {
            UserId = userId;
            Roles = roles;
        }

        public Guid? UserId { get; }
        public string? Username => "tester";
        public IReadOnlyList<string> Roles { get; }
        public string? IpAddress => "127.0.0.1";
    }
}
