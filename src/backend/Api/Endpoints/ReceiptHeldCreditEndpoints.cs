using CongNoGolden.Api;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.ReceiptHeldCredits;
using Microsoft.AspNetCore.Mvc;

namespace CongNoGolden.Api.Endpoints;

public static class ReceiptHeldCreditEndpoints
{
    public static IEndpointRouteBuilder MapReceiptHeldCreditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/customers/{taxCode}/held-credits", async (
            string taxCode,
            string? status,
            string? search,
            string? documentNo,
            string? receiptNo,
            DateOnly? from,
            DateOnly? to,
            int? page,
            int? pageSize,
            IReceiptHeldCreditService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ListByCustomerAsync(
                    taxCode,
                    new ReceiptHeldCreditListRequest(
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
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("CustomerHeldCredits")
        .WithTags("Customers", "HeldCredits")
        .RequireAuthorization("CustomerView");

        app.MapPost("/held-credits/{id:guid}/apply", async (
            Guid id,
            [FromBody] ReceiptHeldCreditApplyRequest request,
            IReceiptHeldCreditService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ApplyToInvoiceAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or ConcurrencyException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("HeldCreditApply")
        .WithTags("HeldCredits")
        .RequireAuthorization("ReceiptApprove");

        app.MapPost("/held-credits/{id:guid}/release", async (
            Guid id,
            [FromBody] ReceiptHeldCreditReleaseRequest request,
            IReceiptHeldCreditService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ReleaseToGeneralCreditAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or ConcurrencyException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("HeldCreditRelease")
        .WithTags("HeldCredits")
        .RequireAuthorization("ReceiptApprove");

        return app;
    }
}
