using CongNoGolden.Application.Advances;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed class AdvanceService : IAdvanceService
{
    private readonly ConGNoDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public AdvanceService(ConGNoDbContext db, ICurrentUser currentUser, IAuditService auditService)
    {
        _db = db;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<AdvanceDto> CreateAsync(AdvanceCreateRequest request, CancellationToken ct)
    {
        _currentUser.EnsureUser();

        if (request.Amount <= 0)
        {
            throw new InvalidOperationException("Advance amount must be greater than zero.");
        }

        if (request.AdvanceDate == default)
        {
            throw new InvalidOperationException("Advance date is required.");
        }

        var seller = request.SellerTaxCode?.Trim() ?? string.Empty;
        var customer = request.CustomerTaxCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(seller) || string.IsNullOrWhiteSpace(customer))
        {
            throw new InvalidOperationException("Seller and customer tax code are required.");
        }

        var advanceNo = string.IsNullOrWhiteSpace(request.AdvanceNo)
            ? null
            : request.AdvanceNo.Trim();

        var sellerExists = await _db.Sellers.AnyAsync(s => s.SellerTaxCode == seller, ct);
        if (!sellerExists)
        {
            throw new InvalidOperationException("Seller not found.");
        }

        var customerExists = await _db.Customers.AnyAsync(c => c.TaxCode == customer, ct);
        if (!customerExists)
        {
            throw new InvalidOperationException("Customer not found.");
        }

        var advance = new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller,
            CustomerTaxCode = customer,
            AdvanceNo = advanceNo,
            AdvanceDate = request.AdvanceDate,
            Amount = request.Amount,
            OutstandingAmount = request.Amount,
            Description = request.Description,
            Status = "DRAFT",
            CreatedBy = _currentUser.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        _db.Advances.Add(advance);
        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            "ADVANCE_CREATE",
            "Advance",
            advance.Id.ToString(),
            null,
            new { advance.Status, advance.Amount, advance.AdvanceDate, advance.AdvanceNo },
            ct);

        return Map(advance);
    }

    public async Task<AdvanceDto> ApproveAsync(Guid advanceId, AdvanceApproveRequest request, CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var advance = await _db.Advances.FirstOrDefaultAsync(a => a.Id == advanceId && a.DeletedAt == null, ct);
        if (advance is null)
        {
            throw new InvalidOperationException("Advance not found.");
        }

        if (request.Version is null)
        {
            throw new InvalidOperationException("Advance version is required.");
        }

        if (request.Version.Value != advance.Version)
        {
            throw new ConcurrencyException("Advance was updated by another user. Please refresh.");
        }

        if (advance.Status != "DRAFT")
        {
            throw new InvalidOperationException("Advance status is not eligible for approval.");
        }

        await EnsureCanApproveAdvance(advance, ct);

        var lockedPeriods = await AdvancePeriodLock.GetLockedPeriodsAsync(_db, advance, ct);
        var overrideApplied = false;
        var overrideReason = string.Empty;
        if (lockedPeriods.Count > 0)
        {
            if (!request.OverridePeriodLock)
            {
                throw new InvalidOperationException(
                    $"Period is locked for advance approval: {string.Join(", ", lockedPeriods)}.");
            }

            overrideReason = PeriodLockOverridePolicy.RequireOverride(_currentUser, request.OverrideReason);
            overrideApplied = true;
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.TaxCode == advance.CustomerTaxCode, ct);
        if (customer is null)
        {
            throw new InvalidOperationException("Customer not found.");
        }

        var previousStatus = advance.Status;
        var now = DateTimeOffset.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        advance.Status = "APPROVED";
        advance.ApprovedAt = now;
        advance.ApprovedBy = _currentUser.UserId;
        advance.OutstandingAmount = advance.Amount;
        advance.UpdatedAt = now;
        advance.Version += 1;
        customer.CurrentBalance += advance.Amount;

        var allocatedAmount = await ApplyReceiptCreditsToAdvanceAsync(advance, now, ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (overrideApplied)
        {
            await _auditService.LogAsync(
                "PERIOD_LOCK_OVERRIDE",
                "Advance",
                advance.Id.ToString(),
                null,
                new { operation = "ADVANCE_APPROVE", lockedPeriods, reason = overrideReason },
                ct);
        }

        await _auditService.LogAsync(
            "ADVANCE_APPROVE",
            "Advance",
            advance.Id.ToString(),
            new { status = previousStatus },
            new { status = advance.Status, advance.Amount, allocatedAmount },
            ct);

        return Map(advance);
    }

    public async Task<AdvanceDto> VoidAsync(Guid advanceId, AdvanceVoidRequest request, CancellationToken ct)
    {
        _currentUser.EnsureUser();

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Void reason is required.");
        }

        var advance = await _db.Advances.FirstOrDefaultAsync(a => a.Id == advanceId && a.DeletedAt == null, ct);
        if (advance is null)
        {
            throw new InvalidOperationException("Advance not found.");
        }

        if (request.Version is null)
        {
            throw new InvalidOperationException("Advance version is required.");
        }

        if (request.Version.Value != advance.Version)
        {
            throw new ConcurrencyException("Advance was updated by another user. Please refresh.");
        }

        if (advance.Status == "VOID")
        {
            throw new InvalidOperationException("Advance already voided.");
        }

        await EnsureCanApproveAdvance(advance, ct);

        var lockedPeriods = await AdvancePeriodLock.GetLockedPeriodsAsync(_db, advance, ct);
        var overrideApplied = false;
        var overrideReason = string.Empty;
        if (lockedPeriods.Count > 0)
        {
            if (!request.OverridePeriodLock)
            {
                throw new InvalidOperationException(
                    $"Period is locked for advance void: {string.Join(", ", lockedPeriods)}.");
            }

            overrideReason = PeriodLockOverridePolicy.RequireOverride(_currentUser, request.OverrideReason);
            overrideApplied = true;
        }

        if (advance.Status == "APPROVED")
        {
            var hasAllocations = await _db.ReceiptAllocations.AnyAsync(r => r.AdvanceId == advance.Id, ct);
            if (hasAllocations)
            {
                throw new InvalidOperationException("Advance has allocations and cannot be voided.");
            }

            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.TaxCode == advance.CustomerTaxCode, ct);
            if (customer is not null)
            {
                customer.CurrentBalance -= advance.Amount;
            }
        }

        var previousStatus = advance.Status;

        advance.Status = "VOID";
        advance.OutstandingAmount = 0;
        advance.DeletedAt = DateTimeOffset.UtcNow;
        advance.DeletedBy = _currentUser.UserId;
        advance.UpdatedAt = DateTimeOffset.UtcNow;
        advance.Version += 1;

        await _db.SaveChangesAsync(ct);

        if (overrideApplied)
        {
            await _auditService.LogAsync(
                "PERIOD_LOCK_OVERRIDE",
                "Advance",
                advance.Id.ToString(),
                null,
                new { operation = "ADVANCE_VOID", lockedPeriods, reason = overrideReason },
                ct);
        }

        await _auditService.LogAsync(
            "ADVANCE_VOID",
            "Advance",
            advance.Id.ToString(),
            new { status = previousStatus },
            new { status = advance.Status, reason = request.Reason },
            ct);

        return Map(advance);
    }

    public async Task<AdvanceDto> UnvoidAsync(Guid advanceId, AdvanceUnvoidRequest request, CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var advance = await _db.Advances.FirstOrDefaultAsync(a => a.Id == advanceId, ct);
        if (advance is null)
        {
            throw new InvalidOperationException("Advance not found.");
        }

        if (request.Version is null)
        {
            throw new InvalidOperationException("Advance version is required.");
        }

        if (request.Version.Value != advance.Version)
        {
            throw new ConcurrencyException("Advance was updated by another user. Please refresh.");
        }

        if (advance.Status != "VOID")
        {
            throw new InvalidOperationException("Advance is not voided.");
        }

        await EnsureCanApproveAdvance(advance, ct);

        var lockedPeriods = await AdvancePeriodLock.GetLockedPeriodsAsync(_db, advance, ct);
        var overrideApplied = false;
        var overrideReason = string.Empty;
        if (lockedPeriods.Count > 0)
        {
            if (!request.OverridePeriodLock)
            {
                throw new InvalidOperationException(
                    $"Period is locked for advance unvoid: {string.Join(", ", lockedPeriods)}.");
            }

            overrideReason = PeriodLockOverridePolicy.RequireOverride(_currentUser, request.OverrideReason);
            overrideApplied = true;
        }

        var hasAllocations = await _db.ReceiptAllocations.AnyAsync(r => r.AdvanceId == advance.Id, ct);
        if (hasAllocations)
        {
            throw new InvalidOperationException("Advance has allocations and cannot be unvoided.");
        }

        var previousStatus = advance.Status;
        advance.Status = "DRAFT";
        advance.OutstandingAmount = advance.Amount;
        advance.DeletedAt = null;
        advance.DeletedBy = null;
        advance.UpdatedAt = DateTimeOffset.UtcNow;
        advance.Version += 1;

        await _db.SaveChangesAsync(ct);

        if (overrideApplied)
        {
            await _auditService.LogAsync(
                "PERIOD_LOCK_OVERRIDE",
                "Advance",
                advance.Id.ToString(),
                null,
                new { operation = "ADVANCE_UNVOID", lockedPeriods, reason = overrideReason },
                ct);
        }

        await _auditService.LogAsync(
            "ADVANCE_UNVOID",
            "Advance",
            advance.Id.ToString(),
            new { status = previousStatus },
            new { status = advance.Status },
            ct);

        return Map(advance);
    }

    public async Task<AdvanceUpdateResult> UpdateAsync(Guid advanceId, AdvanceUpdateRequest request, CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var advance = await _db.Advances.FirstOrDefaultAsync(a => a.Id == advanceId && a.DeletedAt == null, ct);
        if (advance is null)
        {
            throw new InvalidOperationException("Advance not found.");
        }

        if (request.Version is null)
        {
            throw new InvalidOperationException("Advance version is required.");
        }

        if (request.Version.Value != advance.Version)
        {
            throw new ConcurrencyException("Advance was updated by another user. Please refresh.");
        }

        if (advance.Status == "VOID")
        {
            throw new InvalidOperationException("Advance already voided.");
        }

        await EnsureCanApproveAdvance(advance, ct);

        var before = new { advance.Description };
        var nextDescription = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        advance.Description = nextDescription;
        advance.UpdatedAt = DateTimeOffset.UtcNow;
        advance.Version += 1;

        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            "ADVANCE_UPDATE",
            "Advance",
            advance.Id.ToString(),
            before,
            new { advance.Description },
            ct);

        return new AdvanceUpdateResult(advance.Id, advance.Version, advance.Description);
    }

    public async Task<PagedResult<AdvanceListItem>> ListAsync(AdvanceListRequest request, CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);
        var statusFilter = request.Status?.Trim().ToUpperInvariant();
        var includeVoided = statusFilter == "VOID";

        var query = _db.Advances.AsNoTracking();
        query = includeVoided
            ? query.Where(a => a.DeletedAt != null)
            : query.Where(a => a.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(request.SellerTaxCode))
        {
            var seller = request.SellerTaxCode.Trim();
            query = query.Where(a => a.SellerTaxCode == seller);
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerTaxCode))
        {
            var customer = request.CustomerTaxCode.Trim();
            query = query.Where(a => a.CustomerTaxCode == customer);
        }

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(a => a.Status == statusFilter);
        }

        if (!string.IsNullOrWhiteSpace(request.AdvanceNo))
        {
            var advanceNo = request.AdvanceNo.Trim();
            query = query.Where(a =>
                a.AdvanceNo != null &&
                EF.Functions.ILike(a.AdvanceNo, $"%{advanceNo}%"));
        }

        if (request.From.HasValue)
        {
            query = query.Where(a => a.AdvanceDate >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(a => a.AdvanceDate <= request.To.Value);
        }

        if (request.AmountMin.HasValue)
        {
            query = query.Where(a => a.Amount >= request.AmountMin.Value);
        }

        if (request.AmountMax.HasValue)
        {
            query = query.Where(a => a.Amount <= request.AmountMax.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            var source = request.Source.Trim().ToUpperInvariant();
            if (source == "IMPORT")
            {
                query = query.Where(a => a.SourceBatchId != null);
            }
            else if (source == "MANUAL")
            {
                query = query.Where(a => a.SourceBatchId == null);
            }
        }

        var isAdmin = _currentUser.HasAnyRole("Admin", "Supervisor");
        var userId = _currentUser.EnsureUser();

        if (!isAdmin)
        {
            query = query.Where(a => _db.Customers
                .Any(c => c.TaxCode == a.CustomerTaxCode && c.AccountantOwnerId == userId));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(a => a.AdvanceDate)
            .ThenByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.Status,
                a.Version,
                a.AdvanceNo,
                a.AdvanceDate,
                a.Amount,
                a.OutstandingAmount,
                a.SellerTaxCode,
                a.CustomerTaxCode,
                a.Description,
                a.SourceBatchId
            })
            .ToListAsync(ct);

        var customerKeys = rows.Select(r => r.CustomerTaxCode).Distinct().ToList();
        var customers = await _db.Customers
            .AsNoTracking()
            .Where(c => customerKeys.Contains(c.TaxCode))
            .Select(c => new { c.TaxCode, c.Name, c.AccountantOwnerId })
            .ToListAsync(ct);

        var ownerIds = customers
            .Where(c => c.AccountantOwnerId.HasValue)
            .Select(c => c.AccountantOwnerId!.Value)
            .Distinct()
            .ToList();

        var owners = ownerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users
                .Where(u => ownerIds.Contains(u.Id))
                .ToDictionaryAsync(
                    u => u.Id,
                    u => string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName,
                    ct);

        var customerLookup = customers.ToDictionary(c => c.TaxCode, c => c);

        var items = rows.Select(r =>
        {
            customerLookup.TryGetValue(r.CustomerTaxCode, out var customer);
            var ownerName = customer?.AccountantOwnerId.HasValue == true &&
                owners.TryGetValue(customer.AccountantOwnerId.Value, out var name)
                ? name
                : null;

            var canManage = isAdmin || customer?.AccountantOwnerId == userId;
            var sourceType = r.SourceBatchId.HasValue ? "IMPORT" : "MANUAL";

            return new AdvanceListItem(
                r.Id,
                r.Status,
                r.Version,
                r.AdvanceNo,
                r.AdvanceDate,
                r.Amount,
                r.OutstandingAmount,
                r.SellerTaxCode,
                r.CustomerTaxCode,
                r.Description,
                customer?.Name,
                ownerName,
                sourceType,
                r.SourceBatchId,
                canManage);
        }).ToList();

        return new PagedResult<AdvanceListItem>(items, page, pageSize, total);
    }

    private async Task EnsureCanApproveAdvance(Advance advance, CancellationToken ct)
    {
        var userId = _currentUser.EnsureUser();
        if (_currentUser.HasAnyRole("Admin", "Supervisor"))
        {
            return;
        }

        if (_currentUser.HasAnyRole("Accountant"))
        {
            var ownerId = await _db.Customers
                .AsNoTracking()
                .Where(c => c.TaxCode == advance.CustomerTaxCode)
                .Select(c => c.AccountantOwnerId)
                .FirstOrDefaultAsync(ct);

            if (ownerId.HasValue && ownerId.Value == userId)
            {
                return;
            }
        }

        throw new UnauthorizedAccessException("Not allowed to manage this advance.");
    }

    private async Task<decimal> ApplyReceiptCreditsToAdvanceAsync(
        Advance advance,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (advance.OutstandingAmount <= 0)
        {
            return 0m;
        }

        var receipts = await _db.Receipts
            .Where(r => r.DeletedAt == null && r.Status == "APPROVED")
            .Where(r => r.SellerTaxCode == advance.SellerTaxCode && r.CustomerTaxCode == advance.CustomerTaxCode)
            .Where(r => r.UnallocatedAmount > 0)
            .OrderBy(r => r.ReceiptDate)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(ct);

        if (receipts.Count == 0)
        {
            return 0m;
        }

        var remaining = advance.OutstandingAmount;
        var allocatedTotal = 0m;

        foreach (var receipt in receipts)
        {
            if (remaining <= 0)
            {
                break;
            }

            var allocated = Math.Min(remaining, receipt.UnallocatedAmount);
            if (allocated <= 0)
            {
                continue;
            }

            receipt.UnallocatedAmount -= allocated;
            receipt.AllocationStatus = receipt.UnallocatedAmount == 0 ? "ALLOCATED" : "PARTIAL";
            receipt.UpdatedAt = now;
            receipt.Version += 1;

            _db.ReceiptAllocations.Add(new ReceiptAllocation
            {
                Id = Guid.NewGuid(),
                ReceiptId = receipt.Id,
                TargetType = "ADVANCE",
                AdvanceId = advance.Id,
                Amount = allocated,
                CreatedAt = now
            });

            remaining -= allocated;
            allocatedTotal += allocated;
        }

        if (allocatedTotal > 0)
        {
            advance.OutstandingAmount = remaining;
            advance.Status = remaining == 0 ? "PAID" : "APPROVED";
            advance.UpdatedAt = now;
            advance.Version += 1;
        }

        return allocatedTotal;
    }

    private static AdvanceDto Map(Advance advance)
    {
        return new AdvanceDto(
            advance.Id,
            advance.Status,
            advance.Version,
            advance.OutstandingAmount,
            advance.AdvanceNo,
            advance.AdvanceDate,
            advance.Amount,
            advance.SellerTaxCode,
            advance.CustomerTaxCode);
    }
}
