using CongNoGolden.Api;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.Invoices;
using Microsoft.AspNetCore.Mvc;

namespace CongNoGolden.Api.Endpoints;

public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/invoices", async (
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] string? documentNo,
            [FromQuery] string? receiptNo,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            IInvoiceService service,
            CancellationToken ct) =>
        {
            var result = await service.ListAsync(
                new InvoiceListRequest(
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
        .WithName("InvoicesList")
        .WithTags("Invoices")
        .RequireAuthorization("CustomerView");

        app.MapPost("/invoices/{id:guid}/void", async (
            Guid id,
            [FromBody] InvoiceVoidRequest request,
            IInvoiceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.VoidAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or ConcurrencyException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("InvoiceVoid")
        .WithTags("Invoices")
        .RequireAuthorization("InvoiceManage");

        return app;
    }
}
