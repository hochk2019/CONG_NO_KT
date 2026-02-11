using CongNoGolden.Api;
using CongNoGolden.Application.Customers;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/customers", async (
            string? search,
            Guid? ownerId,
            string? status,
            int? page,
            int? pageSize,
            ICustomerService service,
            CancellationToken ct) =>
        {
            var result = await service.ListAsync(
                new CustomerListRequest(
                    search,
                    ownerId,
                    status,
                    page.GetValueOrDefault(1),
                    pageSize.GetValueOrDefault(20)),
                ct);

            return Results.Ok(result);
        })
        .WithName("CustomerList")
        .WithTags("Customers")
        .RequireAuthorization("CustomerView");

        app.MapGet("/customers/{taxCode}", async (
            string taxCode,
            ICustomerService service,
            CancellationToken ct) =>
        {
            var result = await service.GetAsync(taxCode, ct);
            return result is null ? ApiErrors.NotFound("Customer not found.") : Results.Ok(result);
        })
        .WithName("CustomerDetail")
        .WithTags("Customers")
        .RequireAuthorization("CustomerView");

        app.MapPut("/customers/{taxCode}", async (
            string taxCode,
            CustomerUpdateRequest request,
            ConGNoDbContext db,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            var key = taxCode.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                return ApiErrors.InvalidRequest("Tax code is required.");
            }

            var customer = await db.Customers.FirstOrDefaultAsync(c => c.TaxCode == key, ct);
            if (customer is null)
            {
                return ApiErrors.NotFound("Customer not found.");
            }

            var name = (request.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return ApiErrors.InvalidRequest("Customer name is required.");
            }

            var status = (request.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (status is not ("ACTIVE" or "INACTIVE"))
            {
                return ApiErrors.InvalidRequest("Invalid customer status.");
            }

            var isStatusChanged = !string.Equals(customer.Status, status, StringComparison.OrdinalIgnoreCase);
            if (isStatusChanged && status == "INACTIVE" && customer.CurrentBalance > 0)
            {
                return ApiErrors.InvalidRequest("Cannot deactivate customer with outstanding balance.");
            }

            if (request.PaymentTermsDays is null || request.PaymentTermsDays < 0)
            {
                return ApiErrors.InvalidRequest("Payment terms must be non-negative.");
            }

            if (request.CreditLimit is not null && request.CreditLimit < 0)
            {
                return ApiErrors.InvalidRequest("Credit limit must be non-negative.");
            }

            Guid? ownerId = request.OwnerId;
            string? ownerName = null;
            if (ownerId.HasValue)
            {
                var owner = await db.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == ownerId.Value, ct);
                if (owner is null)
                {
                    return ApiErrors.InvalidRequest("Owner user not found.");
                }
                ownerName = string.IsNullOrWhiteSpace(owner.FullName) ? owner.Username : owner.FullName;
            }

            Guid? managerId = request.ManagerId;
            string? managerName = null;
            if (managerId.HasValue)
            {
                var manager = await db.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == managerId.Value, ct);
                if (manager is null)
                {
                    return ApiErrors.InvalidRequest("Manager user not found.");
                }
                managerName = string.IsNullOrWhiteSpace(manager.FullName) ? manager.Username : manager.FullName;
            }

            var before = new
            {
                customer.Name,
                customer.Address,
                customer.Email,
                customer.Phone,
                customer.Status,
                customer.PaymentTermsDays,
                customer.CreditLimit,
                customer.AccountantOwnerId,
                customer.ManagerUserId
            };

            customer.Name = name;
            customer.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
            customer.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            customer.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
            customer.Status = status;
            customer.PaymentTermsDays = request.PaymentTermsDays.Value;
            customer.CreditLimit = request.CreditLimit;
            customer.AccountantOwnerId = ownerId;
            customer.ManagerUserId = managerId;
            customer.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);

            await auditService.LogAsync(
                "CUSTOMER_UPDATE",
                "Customer",
                customer.TaxCode,
                before,
                new
                {
                    customer.Name,
                    customer.Address,
                    customer.Email,
                    customer.Phone,
                    customer.Status,
                    customer.PaymentTermsDays,
                    customer.CreditLimit,
                    OwnerId = ownerId,
                    OwnerName = ownerName,
                    ManagerId = managerId,
                    ManagerName = managerName
                },
                ct);

            return Results.NoContent();
        })
        .WithName("CustomerUpdate")
        .WithTags("Customers")
        .RequireAuthorization("CustomerManage");

        app.MapGet("/customers/{taxCode}/invoices", async (
            string taxCode,
            string? status,
            string? search,
            string? documentNo,
            string? receiptNo,
            DateOnly? from,
            DateOnly? to,
            int? page,
            int? pageSize,
            ICustomerService service,
            CancellationToken ct) =>
        {
            var result = await service.ListInvoicesAsync(
                taxCode,
                new CustomerRelationRequest(
                    status,
                    search,
                    documentNo,
                    receiptNo,
                    from,
                    to,
                    page.GetValueOrDefault(1),
                    pageSize.GetValueOrDefault(20)),
                ct);
            return Results.Ok(result);
        })
        .WithName("CustomerInvoices")
        .WithTags("Customers")
        .RequireAuthorization("CustomerView");

        app.MapGet("/customers/{taxCode}/advances", async (
            string taxCode,
            string? status,
            string? search,
            string? documentNo,
            string? receiptNo,
            DateOnly? from,
            DateOnly? to,
            int? page,
            int? pageSize,
            ICustomerService service,
            CancellationToken ct) =>
        {
            var result = await service.ListAdvancesAsync(
                taxCode,
                new CustomerRelationRequest(
                    status,
                    search,
                    documentNo,
                    receiptNo,
                    from,
                    to,
                    page.GetValueOrDefault(1),
                    pageSize.GetValueOrDefault(20)),
                ct);
            return Results.Ok(result);
        })
        .WithName("CustomerAdvances")
        .WithTags("Customers")
        .RequireAuthorization("CustomerView");

        app.MapGet("/customers/{taxCode}/receipts", async (
            string taxCode,
            string? status,
            string? search,
            string? documentNo,
            string? receiptNo,
            DateOnly? from,
            DateOnly? to,
            int? page,
            int? pageSize,
            ICustomerService service,
            CancellationToken ct) =>
        {
            var result = await service.ListReceiptsAsync(
                taxCode,
                new CustomerRelationRequest(
                    status,
                    search,
                    documentNo,
                    receiptNo,
                    from,
                    to,
                    page.GetValueOrDefault(1),
                    pageSize.GetValueOrDefault(20)),
                ct);
            return Results.Ok(result);
        })
        .WithName("CustomerReceipts")
        .WithTags("Customers")
        .RequireAuthorization("CustomerView");

        app.MapPut("/customers/{taxCode}/owner", async (
            string taxCode,
            CustomerOwnerUpdateRequest request,
            ConGNoDbContext db,
            IAuditService auditService,
            CancellationToken ct) =>
        {
            var key = taxCode.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                return ApiErrors.InvalidRequest("Tax code is required.");
            }

            var customer = await db.Customers.FirstOrDefaultAsync(c => c.TaxCode == key, ct);
            if (customer is null)
            {
                return ApiErrors.NotFound("Customer not found.");
            }

            Guid? ownerId = request.OwnerId;
            string? ownerName = null;
            if (ownerId.HasValue)
            {
                var owner = await db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == ownerId.Value, ct);
                if (owner is null)
                {
                    return ApiErrors.InvalidRequest("Owner user not found.");
                }

                ownerName = string.IsNullOrWhiteSpace(owner.FullName) ? owner.Username : owner.FullName;
            }

            var previousOwner = customer.AccountantOwnerId;
            if (previousOwner == ownerId)
            {
                return Results.NoContent();
            }

            customer.AccountantOwnerId = ownerId;
            customer.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            await auditService.LogAsync(
                "CUSTOMER_OWNER_UPDATE",
                "Customer",
                customer.TaxCode,
                new { ownerId = previousOwner },
                new { ownerId, ownerName },
                ct);

            return Results.NoContent();
        })
        .WithName("CustomerOwnerUpdate")
        .WithTags("Customers")
        .RequireAuthorization("CustomerManage");

        return app;
    }
}

public sealed record CustomerOwnerUpdateRequest(Guid? OwnerId);

public sealed record CustomerUpdateRequest(
    string? Name,
    string? Address,
    string? Email,
    string? Phone,
    string? Status,
    int? PaymentTermsDays,
    decimal? CreditLimit,
    Guid? OwnerId,
    Guid? ManagerId);
