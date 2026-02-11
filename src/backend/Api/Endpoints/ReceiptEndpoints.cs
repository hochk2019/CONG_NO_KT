using CongNoGolden.Api;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.Receipts;
using Microsoft.AspNetCore.Mvc;

namespace CongNoGolden.Api.Endpoints;

public static class ReceiptEndpoints
{
    public static IEndpointRouteBuilder MapReceiptEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/receipts", async (
            string? sellerTaxCode,
            string? customerTaxCode,
            string? status,
            string? allocationStatus,
            string? documentNo,
            DateOnly? from,
            DateOnly? to,
            decimal? amountMin,
            decimal? amountMax,
            string? method,
            string? allocationPriority,
            bool? reminderEnabled,
            int? page,
            int? pageSize,
            IReceiptService service,
            CancellationToken ct) =>
        {
            if (from.HasValue && to.HasValue && from.Value > to.Value)
            {
                return Results.BadRequest(new { message = "Ngày chứng từ từ phải nhỏ hơn hoặc bằng ngày chứng từ đến." });
            }

            if (amountMin.HasValue && amountMax.HasValue && amountMin.Value > amountMax.Value)
            {
                return Results.BadRequest(new { message = "Số tiền từ phải nhỏ hơn hoặc bằng số tiền đến." });
            }

            var result = await service.ListAsync(
                new ReceiptListRequest(
                    sellerTaxCode,
                    customerTaxCode,
                    status,
                    allocationStatus,
                    documentNo,
                    from,
                    to,
                    amountMin,
                    amountMax,
                    method,
                    allocationPriority,
                    reminderEnabled,
                    page.GetValueOrDefault(1),
                    pageSize.GetValueOrDefault(20)),
                ct);

            return Results.Ok(result);
        })
        .WithName("ReceiptList")
        .WithTags("Receipts")
        .RequireAuthorization("ReceiptApprove");

        app.MapGet("/receipts/{id:guid}", async (
            Guid id,
            IReceiptService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetAsync(id, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("ReceiptGet")
        .WithTags("Receipts")
        .RequireAuthorization("ReceiptApprove");

        app.MapPost("/receipts", async (
            [FromBody] ReceiptCreateRequest request,
            IReceiptService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.CreateAsync(request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or ConcurrencyException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("ReceiptCreate")
        .WithTags("Receipts")
        .RequireAuthorization("ReceiptApprove");

        app.MapGet("/receipts/open-items", async (
            string? sellerTaxCode,
            string? customerTaxCode,
            IReceiptService service,
            CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sellerTaxCode) || string.IsNullOrWhiteSpace(customerTaxCode))
                {
                    return Results.Ok(Array.Empty<ReceiptOpenItemDto>());
                }

                var result = await service.ListOpenItemsAsync(
                    sellerTaxCode,
                    customerTaxCode,
                    ct);

                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("ReceiptOpenItems")
        .WithTags("Receipts")
        .RequireAuthorization("ReceiptApprove");

        app.MapPost("/receipts/preview", async (
            ReceiptPreviewRequest request,
            IReceiptService service,
            CancellationToken ct) =>
        {
            var result = await service.PreviewAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("ReceiptPreview")
        .WithTags("Receipts")
        .RequireAuthorization("ReceiptApprove");

        app.MapPost("/receipts/{id:guid}/approve", async (
            Guid id,
            [FromBody] ReceiptApproveRequest request,
            IReceiptService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ApproveAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or ConcurrencyException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("ReceiptApprove")
        .WithTags("Receipts")
        .RequireAuthorization("ReceiptApprove");

        app.MapPost("/receipts/{id:guid}/void", async (
            Guid id,
            [FromBody] ReceiptVoidRequest request,
            IReceiptService service,
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
        .WithName("ReceiptVoid")
        .WithTags("Receipts")
        .RequireAuthorization("ReceiptApprove");

        app.MapGet("/receipts/{id:guid}/allocations", async (
            Guid id,
            IReceiptService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ListAllocationsAsync(id, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("ReceiptAllocations")
        .WithTags("Receipts")
        .RequireAuthorization("ReceiptApprove");

        app.MapPost("/receipts/{id:guid}/reminder", async (
            Guid id,
            [FromBody] ReceiptReminderUpdateRequest request,
            IReceiptService service,
            CancellationToken ct) =>
        {
            try
            {
                await service.UpdateReminderAsync(id, request, ct);
                return Results.NoContent();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("ReceiptReminderUpdate")
        .WithTags("Receipts")
        .RequireAuthorization("ReceiptApprove");

        return app;
    }
}
