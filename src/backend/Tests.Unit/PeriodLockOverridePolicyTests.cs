using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Services;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public class PeriodLockOverridePolicyTests
{
    [Fact]
    public void RequireOverride_Throws_When_Not_Admin_Or_Supervisor()
    {
        var user = new TestUser(new[] { "Accountant" });

        Assert.Throws<UnauthorizedAccessException>(() =>
            PeriodLockOverridePolicy.RequireOverride(user, "reason"));
    }

    [Fact]
    public void RequireOverride_Throws_When_Reason_Missing()
    {
        var user = new TestUser(new[] { "Admin" });

        Assert.Throws<InvalidOperationException>(() =>
            PeriodLockOverridePolicy.RequireOverride(user, " "));
    }

    [Fact]
    public void RequireOverride_Returns_Trimmed_Reason()
    {
        var user = new TestUser(new[] { "Supervisor" });

        var result = PeriodLockOverridePolicy.RequireOverride(user, "  ok  ");

        Assert.Equal("ok", result);
    }

    private sealed class TestUser : ICurrentUser
    {
        public TestUser(IReadOnlyList<string> roles)
        {
            Roles = roles;
        }

        public Guid? UserId => Guid.NewGuid();
        public string? Username => "tester";
        public IReadOnlyList<string> Roles { get; }
        public string? IpAddress => "127.0.0.1";
    }
}
