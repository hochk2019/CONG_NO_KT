using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Imports;
using CongNoGolden.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed class ImportCancelService : IImportCancelService
{
    private const string StatusStaging = "STAGING";
    private const string StatusCancelled = "CANCELLED";

    private readonly ConGNoDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public ImportCancelService(ConGNoDbContext db, ICurrentUser currentUser, IAuditService auditService)
    {
        _db = db;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<ImportCancelResult> CancelAsync(Guid batchId, ImportCancelRequest request, CancellationToken ct)
    {
        var batch = await _db.ImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null)
        {
            throw new InvalidOperationException("Batch not found.");
        }

        if (batch.Status == StatusCancelled)
        {
            return new ImportCancelResult(0);
        }

        if (batch.Status != StatusStaging)
        {
            throw new InvalidOperationException("Batch status is not eligible for cancel.");
        }

        var previousStatus = batch.Status;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var deletedRows = await _db.ImportStagingRows
            .Where(r => r.BatchId == batchId)
            .ExecuteDeleteAsync(ct);

        batch.Status = StatusCancelled;
        batch.CancelledAt = DateTimeOffset.UtcNow;
        batch.CancelledBy = _currentUser.UserId;
        batch.CancelReason = reason;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _auditService.LogAsync(
            "IMPORT_CANCEL",
            "ImportBatch",
            batch.Id.ToString(),
            new { status = previousStatus },
            new { status = batch.Status, deletedRows, reason },
            ct);

        return new ImportCancelResult(deletedRows);
    }
}
