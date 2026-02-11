using System.Security.Cryptography;
using CongNoGolden.Api;
using CongNoGolden.Application.Imports;
using CongNoGolden.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Api.Endpoints;

public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/imports/upload", async (
            [FromForm] ImportUploadRequest request,
            IImportBatchService batchService,
            IImportStagingService stagingService,
            ConGNoDbContext db,
            CancellationToken ct) =>
        {
            try
            {
                var type = (request.Type ?? string.Empty).Trim().ToUpperInvariant();
                if (type is not ("INVOICE" or "ADVANCE" or "RECEIPT"))
                {
                    return ApiErrors.InvalidRequest("Invalid import type.");
                }

                if (request.File is null || request.File.Length == 0)
                {
                    return ApiErrors.InvalidRequest("File is required.");
                }

                string fileHash;
                using (var sha256 = SHA256.Create())
                {
                    await using var stream = request.File.OpenReadStream();
                    var hashBytes = await sha256.ComputeHashAsync(stream, ct);
                    fileHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                }

                var createRequest = new CreateImportBatchRequest(
                    type,
                    "UPLOAD",
                    request.PeriodFrom,
                    request.PeriodTo,
                    request.File.FileName,
                    fileHash,
                    request.IdempotencyKey
                );

                var batch = await batchService.CreateBatchAsync(createRequest, ct);

                var existingCounts = await db.ImportStagingRows
                    .Where(r => r.BatchId == batch.BatchId)
                    .GroupBy(r => r.ValidationStatus)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync(ct);

                ImportStagingResult staging;
                if (existingCounts.Count > 0)
                {
                    var ok = existingCounts.FirstOrDefault(c => c.Status == "OK")?.Count ?? 0;
                    var warn = existingCounts.FirstOrDefault(c => c.Status == "WARN")?.Count ?? 0;
                    var error = existingCounts.FirstOrDefault(c => c.Status == "ERROR")?.Count ?? 0;
                    var total = ok + warn + error;
                    staging = new ImportStagingResult(total, ok, warn, error);
                }
                else
                {
                    await using var parseStream = request.File.OpenReadStream();
                    staging = await stagingService.StageAsync(batch.BatchId, type, parseStream, ct);
                }

                return Results.Ok(new ImportUploadResponse(batch, staging));
            }
            catch (InvalidOperationException ex)
            {
                return ApiErrors.FromException(ex);
            }
            catch (Exception ex)
            {
                return ApiErrors.InvalidRequest($"Không đọc được file import: {ex.Message}", "IMPORT_PARSE_FAILED");
            }
        })
        .WithName("ImportUpload")
        .WithTags("Imports")
        .RequireAuthorization("ImportUpload")
        .DisableAntiforgery();

        app.MapGet("/imports/{batchId:guid}/preview", async (
            Guid batchId,
            string? status,
            int? page,
            int? pageSize,
            IImportPreviewService previewService,
            CancellationToken ct) =>
        {
            var normalized = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
            var pageValue = page.GetValueOrDefault(1);
            var sizeValue = pageSize.GetValueOrDefault(50);

            if (pageValue <= 0 || sizeValue <= 0 || sizeValue > 200)
            {
                return ApiErrors.InvalidRequest("Invalid paging parameters.");
            }

            var result = await previewService.PreviewAsync(batchId, normalized, pageValue, sizeValue, ct);
            return Results.Ok(result);
        })
        .WithName("ImportPreview")
        .WithTags("Imports")
        .RequireAuthorization("ImportUpload");

        app.MapGet("/imports/batches", async (
            string? type,
            string? status,
            string? search,
            int? page,
            int? pageSize,
            IImportBatchService batchService,
            CancellationToken ct) =>
        {
            var request = new ImportBatchListRequest(
                type,
                status,
                search,
                page.GetValueOrDefault(1),
                pageSize.GetValueOrDefault(20));

            var result = await batchService.ListAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("ImportBatchList")
        .WithTags("Imports")
        .RequireAuthorization("ImportHistory");

        app.MapPost("/imports/{batchId:guid}/commit", async (
            Guid batchId,
            ImportCommitRequest? request,
            IImportCommitService commitService,
            CancellationToken ct) =>
        {
            try
            {
                var result = await commitService.CommitAsync(batchId, request ?? new ImportCommitRequest(null), ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("ImportCommit")
        .WithTags("Imports")
        .RequireAuthorization("ImportCommit");

        app.MapPost("/imports/{batchId:guid}/rollback", async (
            Guid batchId,
            ImportRollbackRequest? request,
            IImportRollbackService rollbackService,
            CancellationToken ct) =>
        {
            try
            {
                var result = await rollbackService.RollbackAsync(batchId, request ?? new ImportRollbackRequest(), ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("ImportRollback")
        .WithTags("Imports")
        .RequireAuthorization("ImportCommit");

        app.MapPost("/imports/{batchId:guid}/cancel", async (
            Guid batchId,
            ImportCancelRequest? request,
            IImportCancelService cancelService,
            CancellationToken ct) =>
        {
            try
            {
                var result = await cancelService.CancelAsync(batchId, request ?? new ImportCancelRequest(), ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
            {
                return ApiErrors.FromException(ex);
            }
        })
        .WithName("ImportCancel")
        .WithTags("Imports")
        .RequireAuthorization("ImportHistory");

        return app;
    }
}

public sealed class ImportUploadRequest
{
    public string? Type { get; init; }
    public DateOnly? PeriodFrom { get; init; }
    public DateOnly? PeriodTo { get; init; }
    public Guid? IdempotencyKey { get; init; }
    public IFormFile? File { get; init; }
}

public sealed record ImportUploadResponse(
    ImportBatchDto Batch,
    ImportStagingResult Staging
);
