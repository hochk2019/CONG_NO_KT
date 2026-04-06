using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services.Common;

namespace CongNoGolden.Infrastructure.Security;

public static class CustomerPermissionEvaluator
{
    public static bool CanEditCustomer(ICurrentUser currentUser, Customer customer)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(customer);

        if (currentUser.HasAnyPermission(AppPermissions.CustomerEditAll))
        {
            return true;
        }

        if (customer.AccountantOwnerId is null)
        {
            return currentUser.HasAnyPermission(AppPermissions.CustomerEditUnassigned);
        }

        return currentUser.UserId == customer.AccountantOwnerId
            && currentUser.HasAnyPermission(AppPermissions.CustomerEditOwned);
    }

    public static bool CanUpdateCustomer(
        ICurrentUser currentUser,
        Customer customer,
        Guid? requestedOwnerId,
        Guid? requestedManagerId)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(customer);

        if (!CanEditCustomer(currentUser, customer))
        {
            return false;
        }

        var assignmentsChanged =
            customer.AccountantOwnerId != requestedOwnerId
            || customer.ManagerUserId != requestedManagerId;

        return !assignmentsChanged
            || CanManageAssignments(currentUser)
            || CanClaimUnassignedCustomer(currentUser, customer, requestedOwnerId, requestedManagerId);
    }

    public static bool CanManageAssignments(ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        return currentUser.HasAnyPermission(
            AppPermissions.CustomerEditAll,
            AppPermissions.CustomerAssignmentManage);
    }

    private static bool CanClaimUnassignedCustomer(
        ICurrentUser currentUser,
        Customer customer,
        Guid? requestedOwnerId,
        Guid? requestedManagerId)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(customer);

        // Users who can edit unassigned customers may claim the customer for themselves,
        // but still cannot reassign to someone else or change the manager.
        if (customer.AccountantOwnerId is not null || currentUser.UserId is null)
        {
            return false;
        }

        return requestedOwnerId == currentUser.UserId
            && requestedManagerId == customer.ManagerUserId;
    }
}

