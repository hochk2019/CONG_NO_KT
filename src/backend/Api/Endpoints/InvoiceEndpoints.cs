using CongNoGolden.Api;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.Invoices;
using Microsoft.AspNetCore.Mvc;

namespace CongNoGolden.Api.Endpoints;

public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
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
