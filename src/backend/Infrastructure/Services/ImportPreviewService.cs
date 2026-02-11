using System.Text.Json;
using CongNoGolden.Application.Imports;
using CongNoGolden.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed class ImportPreviewService : IImportPreviewService
{
    private readonly ConGNoDbContext _db;

    public ImportPreviewService(ConGNoDbContext db)
    {
        _db = db;
    }

    public async Task<ImportPreviewResult> PreviewAsync(Guid batchId, string? status, int page, int pageSize, CancellationToken ct)
    {
        var baseQuery = _db.ImportStagingRows.AsNoTracking().Where(r => r.BatchId == batchId);

        var counts = await baseQuery
            .GroupBy(r => r.ValidationStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int ok = counts.FirstOrDefault(c => c.Status == ImportStagingHelpers.StatusOk)?.Count ?? 0;
        int warn = counts.FirstOrDefault(c => c.Status == ImportStagingHelpers.StatusWarn)?.Count ?? 0;
        int error = counts.FirstOrDefault(c => c.Status == ImportStagingHelpers.StatusError)?.Count ?? 0;
        int total = ok + warn + error;

        if (!string.IsNullOrWhiteSpace(status))
        {
            baseQuery = baseQuery.Where(r => r.ValidationStatus == status);
        }

        var rows = await baseQuery
            .OrderBy(r => r.RowNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var mapped = rows.Select(r => new ImportPreviewRow(
            r.RowNo,
            r.ValidationStatus,
            JsonDocument.Parse(r.RawData).RootElement.Clone(),
            JsonDocument.Parse(r.ValidationMessages ?? "[]").RootElement.Clone(),
            r.DedupKey,
            r.ActionSuggestion
        )).ToList();

        return new ImportPreviewResult(total, ok, warn, error, page, pageSize, mapped);
    }
}
