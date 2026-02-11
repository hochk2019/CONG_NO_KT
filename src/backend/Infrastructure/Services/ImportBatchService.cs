using System.Text.Json;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Imports;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
namespace CongNoGolden.Infrastructure.Services;

public sealed class ImportBatchService : IImportBatchService
{
    private readonly ConGNoDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ImportBatchService(ConGNoDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ImportBatchDto> CreateBatchAsync(CreateImportBatchRequest request, CancellationToken ct)
    {
        if (request.IdempotencyKey.HasValue)
        {
            var existing = await _db.ImportBatches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.IdempotencyKey == request.IdempotencyKey, ct);

            if (existing is not null)
            {
                var sameType = string.Equals(existing.Type, request.Type, StringComparison.OrdinalIgnoreCase);
                var sameSource = string.Equals(existing.Source, request.Source, StringComparison.OrdinalIgnoreCase);
                var sameHash = string.Equals(existing.FileHash ?? string.Empty, request.FileHash ?? string.Empty, StringComparison.OrdinalIgnoreCase);

                if (!sameType || !sameSource || !sameHash)
                {
                    throw new InvalidOperationException("Idempotency key already used for a different batch.");
                }

                return new ImportBatchDto(existing.Id, existing.Status, existing.FileHash);
            }
        }

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            Source = request.Source,
            PeriodFrom = request.PeriodFrom,
            PeriodTo = request.PeriodTo,
            FileName = request.FileName,
            FileHash = request.FileHash,
            IdempotencyKey = request.IdempotencyKey,
            Status = "STAGING",
            CreatedBy = _currentUser.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.ImportBatches.Add(batch);
        await _db.SaveChangesAsync(ct);

        return new ImportBatchDto(batch.Id, batch.Status, batch.FileHash);
    }

    public async Task<PagedResult<ImportBatchListItem>> ListAsync(ImportBatchListRequest request, CancellationToken ct)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);

        var query = _db.ImportBatches.AsNoTracking();
        var search = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var type = request.Type.Trim().ToUpperInvariant();
            query = query.Where(b => b.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToUpperInvariant();
            query = query.Where(b => b.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{search}%";
            var searchUserIds = await _db.Users
                .Where(u =>
                    EF.Functions.ILike(u.Username, like) ||
                    (u.FullName != null && EF.Functions.ILike(u.FullName, like)))
                .Select(u => u.Id)
                .ToListAsync(ct);

            query = query.Where(b =>
                EF.Functions.ILike(b.Type, like) ||
                EF.Functions.ILike(b.Status, like) ||
                EF.Functions.ILike(b.Id.ToString(), like) ||
                (b.FileName != null && EF.Functions.ILike(b.FileName, like)) ||
                (b.CancelReason != null && EF.Functions.ILike(b.CancelReason, like)) ||
                (searchUserIds.Count > 0 &&
                 ((b.CreatedBy.HasValue && searchUserIds.Contains(b.CreatedBy.Value)) ||
                  (b.CancelledBy.HasValue && searchUserIds.Contains(b.CancelledBy.Value)))));
        }

        var total = await query.CountAsync(ct);
        var batches = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new
            {
                b.Id,
                b.Type,
                b.Status,
                b.FileName,
                b.PeriodFrom,
                b.PeriodTo,
                b.CreatedAt,
                b.CreatedBy,
                b.CommittedAt,
                b.SummaryData,
                b.CancelledAt,
                b.CancelledBy,
                b.CancelReason
            })
            .ToListAsync(ct);

        var userIds = batches
            .SelectMany(b => new[] { b.CreatedBy, b.CancelledBy })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var userLookup = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(
                    u => u.Id,
                    u => string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName,
                    ct);

        var items = batches.Select(b => new ImportBatchListItem(
                b.Id,
                b.Type,
                b.Status,
                b.FileName,
                b.PeriodFrom,
                b.PeriodTo,
                b.CreatedAt,
                b.CreatedBy.HasValue && userLookup.TryGetValue(b.CreatedBy.Value, out var name) ? name : null,
                b.CommittedAt,
                ParseSummary(b.SummaryData),
                b.CancelledAt,
                b.CancelledBy.HasValue && userLookup.TryGetValue(b.CancelledBy.Value, out var cancelledName)
                    ? cancelledName
                    : null,
                b.CancelReason))
            .ToList();

        return new PagedResult<ImportBatchListItem>(items, page, pageSize, total);
    }

    private static ImportCommitResult ParseSummary(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return new ImportCommitResult(0, 0, 0);
        }

        try
        {
            var result = JsonSerializer.Deserialize<ImportCommitResult>(summary);
            return result ?? new ImportCommitResult(0, 0, 0);
        }
        catch
        {
            return new ImportCommitResult(0, 0, 0);
        }
    }
}
