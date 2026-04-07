using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Security;
using Xunit;

namespace Tests.Unit;

public sealed class ImportPermissionEvaluatorTests
{
    [Theory]
    [InlineData("INVOICE", AppPermissions.ImportCommitInvoice)]
    [InlineData("ADVANCE", AppPermissions.ImportCommitAdvance)]
    [InlineData("RECEIPT", AppPermissions.ImportCommitReceipt)]
    public void CanCommitImport_ReturnsTrue_WhenUserHasMatchingPermission(string importType, string permission)
    {
        var user = new TestCurrentUser([permission]);

        var actual = ImportPermissionEvaluator.CanCommitImport(user, importType);

        Assert.True(actual);
    }

    [Fact]
    public void CanCommitImport_ReturnsFalse_WhenUserLacksMatchingPermission()
    {
        var user = new TestCurrentUser([AppPermissions.ImportCommitInvoice]);

        var actual = ImportPermissionEvaluator.CanCommitImport(user, "RECEIPT");

        Assert.False(actual);
    }

    [Fact]
    public void CanRollbackImport_ReturnsTrue_WhenUserHasRollbackPermission()
    {
        var user = new TestCurrentUser([AppPermissions.ImportRollback]);

        var actual = ImportPermissionEvaluator.CanRollbackImport(user);

        Assert.True(actual);
    }

    [Fact]
    public void CanRollbackImport_ReturnsFalse_WhenUserLacksRollbackPermission()
    {
        var user = new TestCurrentUser([AppPermissions.ImportCommitInvoice]);

        var actual = ImportPermissionEvaluator.CanRollbackImport(user);

        Assert.False(actual);
    }

    [Fact]
    public void ResolveCommitPermission_ReturnsNull_ForUnknownType()
    {
        var actual = ImportPermissionEvaluator.ResolveCommitPermission("OTHER");

        Assert.Null(actual);
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(IReadOnlyList<string>? permissions = null)
        {
            Permissions = permissions ?? Array.Empty<string>();
        }

        public Guid? UserId => Guid.NewGuid();
        public string? Username => "tester";
        public IReadOnlyList<string> Roles => Array.Empty<string>();
        public IReadOnlyList<string> Permissions { get; }
        public string? IpAddress => "127.0.0.1";
    }
}
