using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Security;
using Xunit;

namespace Tests.Unit;

public sealed class CustomerPermissionEvaluatorTests
{
    [Fact]
    public void CanEditCustomer_ReturnsTrue_ForOwnedCustomer_WhenUserHasOwnedPermission()
    {
        var ownerId = Guid.NewGuid();
        var user = new TestCurrentUser(
            ownerId,
            permissions: [AppPermissions.CustomerEditOwned]);
        var customer = new Customer
        {
            TaxCode = "0101234567",
            Name = "Owned customer",
            AccountantOwnerId = ownerId
        };

        var actual = CustomerPermissionEvaluator.CanEditCustomer(user, customer);

        Assert.True(actual);
    }

    [Fact]
    public void CanEditCustomer_ReturnsTrue_ForUnassignedCustomer_WhenUserHasUnassignedPermission()
    {
        var user = new TestCurrentUser(
            Guid.NewGuid(),
            permissions: [AppPermissions.CustomerEditUnassigned]);
        var customer = new Customer
        {
            TaxCode = "0101234568",
            Name = "Unassigned customer",
            AccountantOwnerId = null
        };

        var actual = CustomerPermissionEvaluator.CanEditCustomer(user, customer);

        Assert.True(actual);
    }

    [Fact]
    public void CanEditCustomer_ReturnsFalse_ForOtherOwnersCustomer_WhenUserOnlyHasOwnedPermission()
    {
        var user = new TestCurrentUser(
            Guid.NewGuid(),
            permissions: [AppPermissions.CustomerEditOwned]);
        var customer = new Customer
        {
            TaxCode = "0101234569",
            Name = "Other owner customer",
            AccountantOwnerId = Guid.NewGuid()
        };

        var actual = CustomerPermissionEvaluator.CanEditCustomer(user, customer);

        Assert.False(actual);
    }

    [Fact]
    public void CanManageAssignments_ReturnsFalse_WithoutAssignmentPermission()
    {
        var ownerId = Guid.NewGuid();
        var user = new TestCurrentUser(
            ownerId,
            permissions:
            [
                AppPermissions.CustomerEditOwned,
                AppPermissions.CustomerEditUnassigned
            ]);

        var actual = CustomerPermissionEvaluator.CanManageAssignments(user);

        Assert.False(actual);
    }

    [Fact]
    public void CanManageAssignments_ReturnsTrue_WithAssignmentPermission()
    {
        var user = new TestCurrentUser(
            Guid.NewGuid(),
            permissions: [AppPermissions.CustomerAssignmentManage]);

        var actual = CustomerPermissionEvaluator.CanManageAssignments(user);

        Assert.True(actual);
    }

    [Fact]
    public void CanUpdateCustomer_ReturnsTrue_WhenEditableAndAssignmentsUnchanged()
    {
        var ownerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var user = new TestCurrentUser(
            ownerId,
            permissions: [AppPermissions.CustomerEditOwned]);
        var customer = new Customer
        {
            TaxCode = "0101234570",
            Name = "Existing customer",
            AccountantOwnerId = ownerId,
            ManagerUserId = managerId
        };

        var actual = CustomerPermissionEvaluator.CanUpdateCustomer(
            user,
            customer,
            ownerId,
            managerId);

        Assert.True(actual);
    }

    [Fact]
    public void CanUpdateCustomer_ReturnsFalse_WhenAssignmentsChangeWithoutAssignmentPermission()
    {
        var ownerId = Guid.NewGuid();
        var user = new TestCurrentUser(
            ownerId,
            permissions: [AppPermissions.CustomerEditOwned]);
        var customer = new Customer
        {
            TaxCode = "0101234571",
            Name = "Protected customer",
            AccountantOwnerId = ownerId,
            ManagerUserId = Guid.NewGuid()
        };

        var actual = CustomerPermissionEvaluator.CanUpdateCustomer(
            user,
            customer,
            Guid.NewGuid(),
            customer.ManagerUserId);

        Assert.False(actual);
    }

    [Fact]
    public void CanUpdateCustomer_ReturnsTrue_WhenAssignmentsChangeWithAssignmentPermission()
    {
        var ownerId = Guid.NewGuid();
        var user = new TestCurrentUser(
            ownerId,
            permissions:
            [
                AppPermissions.CustomerEditOwned,
                AppPermissions.CustomerAssignmentManage
            ]);
        var customer = new Customer
        {
            TaxCode = "0101234572",
            Name = "Assignable customer",
            AccountantOwnerId = ownerId,
            ManagerUserId = Guid.NewGuid()
        };

        var actual = CustomerPermissionEvaluator.CanUpdateCustomer(
            user,
            customer,
            Guid.NewGuid(),
            customer.ManagerUserId);

        Assert.True(actual);
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(Guid? userId, IReadOnlyList<string>? permissions = null)
        {
            UserId = userId;
            Permissions = permissions ?? Array.Empty<string>();
        }

        public Guid? UserId { get; }
        public string? Username => "tester";
        public IReadOnlyList<string> Roles => Array.Empty<string>();
        public IReadOnlyList<string> Permissions { get; }
        public string? IpAddress => "127.0.0.1";
    }
}
