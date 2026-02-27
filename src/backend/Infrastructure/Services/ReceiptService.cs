using System.Text.Json;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Common.StatusCodes;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Domain.Allocation;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReceiptService : IReceiptService
{
    private readonly ConGNoDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public ReceiptService(ConGNoDbContext db, ICurrentUser currentUser, IAuditService auditService)
    {
        _db = db;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<PagedResult<ReceiptListItem>> ListAsync(ReceiptListRequest request, CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);
        var statusFilter = request.Status?.Trim().ToUpperInvariant();
        var includeVoided = statusFilter == ReceiptStatusCodes.Void;

        var query = _db.Receipts.AsNoTracking();
        query = includeVoided
            ? query.Where(r => r.DeletedAt != null)
            : query.Where(r => r.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(request.SellerTaxCode))
        {
            var seller = request.SellerTaxCode.Trim();
            query = query.Where(r => r.SellerTaxCode == seller);
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerTaxCode))
        {
            var customer = request.CustomerTaxCode.Trim();
            query = query.Where(r => r.CustomerTaxCode == customer);
        }

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(r => r.Status == statusFilter);
        }

        if (!string.IsNullOrWhiteSpace(request.AllocationStatus))
        {
            var allocationStatus = request.AllocationStatus.Trim().ToUpperInvariant();
            query = allocationStatus switch
            {
                "ALLOCATED" => query.Where(r =>
                    r.AllocationStatus == ReceiptAllocationStatusCodes.Allocated ||
                    r.AllocationStatus == ReceiptAllocationStatusCodes.Partial),
                "UNALLOCATED" => query.Where(r =>
                    r.AllocationStatus == ReceiptAllocationStatusCodes.Unallocated ||
                    r.AllocationStatus == ReceiptAllocationStatusCodes.Selected ||
                    r.AllocationStatus == ReceiptAllocationStatusCodes.Suggested),
                _ => query.Where(r => r.AllocationStatus == allocationStatus)
            };
        }

        if (!string.IsNullOrWhiteSpace(request.DocumentNo))
        {
            var keyword = request.DocumentNo.Trim();
            var pattern = $"%{keyword}%";

            var receiptMatches = _db.Receipts
                .AsNoTracking()
                .Where(r => includeVoided ? r.DeletedAt != null : r.DeletedAt == null)
                .Where(r => r.ReceiptNo != null && EF.Functions.ILike(r.ReceiptNo, pattern))
                .Select(r => r.Id);

            var invoiceMatches = _db.ReceiptAllocations
                .AsNoTracking()
                .Where(a => a.InvoiceId != null)
                .Join(_db.Invoices
                        .AsNoTracking()
                        .Where(i => i.DeletedAt == null && EF.Functions.ILike(i.InvoiceNo, pattern)),
                    allocation => allocation.InvoiceId!,
                    invoice => invoice.Id,
                    (allocation, _) => allocation.ReceiptId);

            var advanceMatches = _db.ReceiptAllocations
                .AsNoTracking()
                .Where(a => a.AdvanceId != null)
                .Join(_db.Advances
                        .AsNoTracking()
                        .Where(a => a.DeletedAt == null && a.AdvanceNo != null && EF.Functions.ILike(a.AdvanceNo, pattern)),
                    allocation => allocation.AdvanceId!,
                    advance => advance.Id,
                    (allocation, _) => allocation.ReceiptId);

            var matchingIds = receiptMatches
                .Concat(invoiceMatches)
                .Concat(advanceMatches)
                .Distinct();

            query = query.Where(r => matchingIds.Contains(r.Id));
        }

        if (request.From.HasValue)
        {
            var from = request.From.Value;
            query = query.Where(r => r.ReceiptDate >= from);
        }

        if (request.To.HasValue)
        {
            var to = request.To.Value;
            query = query.Where(r => r.ReceiptDate <= to);
        }

        if (request.AmountMin.HasValue)
        {
            var amountMin = request.AmountMin.Value;
            query = query.Where(r => r.Amount >= amountMin);
        }

        if (request.AmountMax.HasValue)
        {
            var amountMax = request.AmountMax.Value;
            query = query.Where(r => r.Amount <= amountMax);
        }

        if (!string.IsNullOrWhiteSpace(request.Method))
        {
            var method = request.Method.Trim().ToUpperInvariant();
            query = query.Where(r => r.Method == method);
        }

        if (!string.IsNullOrWhiteSpace(request.AllocationPriority))
        {
            var priority = request.AllocationPriority.Trim().ToUpperInvariant();
            query = query.Where(r => r.AllocationPriority == priority);
        }

        if (request.ReminderEnabled.HasValue)
        {
            query = request.ReminderEnabled.Value
                ? query.Where(r => r.ReminderDisabledAt == null)
                : query.Where(r => r.ReminderDisabledAt != null);
        }

        var ownerFilter = _currentUser.ResolveOwnerFilter(
            privilegedRoles: ["Admin", "Supervisor"]);
        var isAdmin = !ownerFilter.HasValue;

        if (ownerFilter.HasValue)
        {
            var ownerId = ownerFilter.Value;
            query = query.Where(r => _db.Customers
                .Any(c => c.TaxCode == r.CustomerTaxCode && c.AccountantOwnerId == ownerId));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(r => r.ReceiptDate)
            .ThenByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.Status,
                r.Version,
                r.ReceiptNo,
                r.ReceiptDate,
                r.Amount,
                r.UnallocatedAmount,
                r.AllocationMode,
                r.AllocationStatus,
                r.AllocationPriority,
                r.AllocationSource,
                r.AllocationSuggestedAt,
                r.LastReminderAt,
                r.ReminderDisabledAt,
                r.Method,
                r.SellerTaxCode,
                r.CustomerTaxCode
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

            var canManage = isAdmin || customer?.AccountantOwnerId == ownerFilter;

            return new ReceiptListItem(
                r.Id,
                r.Status,
                r.Version,
                r.ReceiptNo,
                r.ReceiptDate,
                r.Amount,
                r.UnallocatedAmount,
                r.AllocationMode,
                r.AllocationStatus,
                r.AllocationPriority,
                r.AllocationSource,
                r.AllocationSuggestedAt,
                r.LastReminderAt,
                r.ReminderDisabledAt,
                r.Method,
                r.SellerTaxCode,
                r.CustomerTaxCode,
                customer?.Name,
                ownerName,
                canManage);
        }).ToList();

        return new PagedResult<ReceiptListItem>(items, page, pageSize, total);
    }

    public async Task<ReceiptDto> CreateAsync(ReceiptCreateRequest request, CancellationToken ct)
    {
        _currentUser.EnsureUser();

        if (request.Amount <= 0)
        {
            throw new InvalidOperationException("Receipt amount must be greater than zero.");
        }

        if (request.ReceiptDate == default)
        {
            throw new InvalidOperationException("Receipt date is required.");
        }

        var seller = request.SellerTaxCode?.Trim() ?? string.Empty;
        var customer = request.CustomerTaxCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(seller) || string.IsNullOrWhiteSpace(customer))
        {
            throw new InvalidOperationException("Seller and customer tax code are required.");
        }

        var receiptNo = string.IsNullOrWhiteSpace(request.ReceiptNo)
            ? null
            : request.ReceiptNo.Trim();

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

        await EnsureCanManageCustomer(customer, ct);

        var allocationMode = NormalizeAllocationMode(request.AllocationMode);
        var allocationPriority = NormalizeAllocationPriority(request.AllocationPriority);
        var appliedPeriodStart = request.AppliedPeriodStart;
        var method = NormalizeMethod(request.Method);
        var selectedTargets = NormalizeSelectedTargets(request.SelectedTargets);
        var openItems = await LoadOpenItemsAsync(seller, customer, ct);

        if (openItems.Count > 0 && selectedTargets.Count == 0)
        {
            throw new InvalidOperationException("Cần chọn chứng từ để phân bổ phiếu thu.");
        }

        if (selectedTargets.Count > 0)
        {
            ValidateSelectedTargets(selectedTargets, openItems);
            allocationMode = "MANUAL";
        }

        if (allocationMode == "BY_PERIOD" && appliedPeriodStart is null)
        {
            throw new InvalidOperationException("Applied period start is required for BY_PERIOD.");
        }

        if (appliedPeriodStart.HasValue && appliedPeriodStart.Value.Day != 1)
        {
            appliedPeriodStart = new DateOnly(appliedPeriodStart.Value.Year, appliedPeriodStart.Value.Month, 1);
        }

        var allocationTargetsJson = selectedTargets.Count > 0
            ? SerializeTargets(selectedTargets)
            : null;
        var allocationStatus = selectedTargets.Count > 0
            ? ReceiptAllocationStatusCodes.Selected
            : ReceiptAllocationStatusCodes.Unallocated;
        var allocationSource = selectedTargets.Count > 0 ? "MANUAL" : null;

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = seller,
            CustomerTaxCode = customer,
            ReceiptNo = receiptNo,
            ReceiptDate = request.ReceiptDate,
            AppliedPeriodStart = appliedPeriodStart,
            Amount = request.Amount,
            Method = method,
            Description = request.Description,
            AllocationMode = allocationMode,
            AllocationStatus = allocationStatus,
            AllocationPriority = allocationPriority,
            AllocationTargets = allocationTargetsJson,
            AllocationSource = allocationSource,
            UnallocatedAmount = 0,
            Status = ReceiptStatusCodes.Draft,
            CreatedBy = _currentUser.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        _db.Receipts.Add(receipt);
        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            "RECEIPT_CREATE",
            "Receipt",
            receipt.Id.ToString(),
            null,
            new { receipt.Status, receipt.Amount, receipt.ReceiptDate, receipt.ReceiptNo },
            ct);

        return new ReceiptDto(
            receipt.Id,
            receipt.Status,
            receipt.Version,
            receipt.Amount,
            receipt.UnallocatedAmount,
            receipt.ReceiptNo,
            receipt.ReceiptDate,
            receipt.AppliedPeriodStart,
            receipt.AllocationMode,
            receipt.AllocationStatus,
            receipt.AllocationPriority,
            receipt.AllocationSource,
            receipt.AllocationSuggestedAt,
            DeserializeTargets(receipt.AllocationTargets),
            receipt.Method,
            receipt.SellerTaxCode,
            receipt.CustomerTaxCode);
    }

    public async Task<ReceiptPreviewResult> PreviewAsync(ReceiptPreviewRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0)
        {
            return new ReceiptPreviewResult(Array.Empty<ReceiptPreviewLine>(), request.Amount);
        }

        var mode = ParseMode(request.AllocationMode);
        var targets = await LoadTargetsAsync(request.SellerTaxCode, request.CustomerTaxCode, ct);
        var selected = MapSelectedTargets(request.SelectedTargets);

        var allocation = AllocationEngine.Allocate(
            new AllocationRequest(request.Amount, mode, request.AppliedPeriodStart, selected),
            targets);

        return new ReceiptPreviewResult(
            allocation.Lines.Select(l => new ReceiptPreviewLine(l.TargetId, l.TargetType.ToString().ToUpperInvariant(), l.Amount)).ToList(),
            allocation.UnallocatedAmount);
    }

    public async Task<ReceiptPreviewResult> ApproveAsync(Guid receiptId, ReceiptApproveRequest request, CancellationToken ct)
    {
        try
        {
            var receipt = await _db.Receipts.FirstOrDefaultAsync(r => r.Id == receiptId && r.DeletedAt == null, ct);
            if (receipt is null)
            {
                throw new InvalidOperationException("Receipt not found.");
            }

            if (request.Version is null)
            {
                throw new InvalidOperationException("Receipt version is required.");
            }

            if (request.Version.Value != receipt.Version)
            {
                throw new ConcurrencyException("Receipt was updated by another user. Please refresh.");
            }

            if (receipt.Status != ReceiptStatusCodes.Draft)
            {
                throw new InvalidOperationException("Receipt status is not eligible for approval.");
            }

            await EnsureCanApproveReceipt(receipt, ct);
            var lockedPeriods = await ReceiptPeriodLock.GetLockedPeriodsAsync(_db, receipt, ct);
            var overrideApplied = false;
            var overrideReason = string.Empty;
            if (lockedPeriods.Count > 0)
            {
                if (!request.OverridePeriodLock)
                {
                    throw new InvalidOperationException(
                        $"Period is locked for receipt approval: {string.Join(", ", lockedPeriods)}.");
                }

                overrideReason = PeriodLockOverridePolicy.RequireOverride(_currentUser, request.OverrideReason);
                overrideApplied = true;
            }

            var mode = ParseMode(receipt.AllocationMode);
            var selected = MapSelectedTargets(request.SelectedTargets ?? DeserializeTargets(receipt.AllocationTargets));

            var previousStatus = receipt.Status;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var targets = await LoadTargetsAsync(receipt.SellerTaxCode, receipt.CustomerTaxCode, ct);
            if (targets.Count > 0 && (selected is null || selected.Count == 0))
            {
                throw new InvalidOperationException("Cần chọn chứng từ để phân bổ phiếu thu.");
            }
            var allocation = AllocationEngine.Allocate(
                new AllocationRequest(receipt.Amount, mode, receipt.AppliedPeriodStart, selected),
                targets);

            var allocatedTotal = allocation.Lines.Sum(l => l.Amount);

            await ApplyAllocations(receipt, allocation.Lines, ct);

            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.TaxCode == receipt.CustomerTaxCode, ct);
            if (customer is not null)
            {
                customer.CurrentBalance -= receipt.Amount;
            }

            var finalTargets = request.SelectedTargets ?? DeserializeTargets(receipt.AllocationTargets);

            receipt.Status = ReceiptStatusCodes.Approved;
            receipt.ApprovedAt = DateTimeOffset.UtcNow;
            receipt.ApprovedBy = _currentUser.UserId;
            receipt.UnallocatedAmount = allocation.UnallocatedAmount;
            receipt.AllocationStatus = allocation.UnallocatedAmount > 0
                ? ReceiptAllocationStatusCodes.Partial
                : ReceiptAllocationStatusCodes.Allocated;
            receipt.AllocationTargets = SerializeTargets(finalTargets);
            if (request.SelectedTargets is not null)
            {
                receipt.AllocationSource = "MANUAL";
            }
            receipt.UpdatedAt = DateTimeOffset.UtcNow;
            receipt.Version += 1;

            await _db.SaveChangesAsync(ct);

            if (overrideApplied)
            {
                await _auditService.LogAsync(
                    "PERIOD_LOCK_OVERRIDE",
                    "Receipt",
                    receipt.Id.ToString(),
                    null,
                    new { operation = "RECEIPT_APPROVE", lockedPeriods, reason = overrideReason },
                    ct);
            }

            await _auditService.LogAsync(
                "RECEIPT_APPROVE",
                "Receipt",
                receipt.Id.ToString(),
                new { status = previousStatus },
                new { status = receipt.Status, allocatedTotal, allocation.UnallocatedAmount },
                ct);

            await tx.CommitAsync(ct);

            if (receipt.AllocationStatus == ReceiptAllocationStatusCodes.Partial &&
                receipt.UnallocatedAmount > 0)
            {
                await NotifyPartialAllocationAsync(receipt, ct);
                await _db.SaveChangesAsync(ct);
            }

            BusinessMetrics.RecordReceiptApprovalSuccess(
                receipt.AllocationStatus,
                receipt.Amount,
                allocation.UnallocatedAmount);

            return new ReceiptPreviewResult(
                allocation.Lines.Select(l => new ReceiptPreviewLine(l.TargetId, l.TargetType.ToString().ToUpperInvariant(), l.Amount)).ToList(),
                allocation.UnallocatedAmount);
        }
        catch (Exception ex) when (ex is ConcurrencyException or InvalidOperationException or UnauthorizedAccessException)
        {
            BusinessMetrics.RecordReceiptApprovalFailure(ResolveReceiptApprovalFailureReason(ex));
            throw;
        }
    }

    private async Task EnsureCanApproveReceipt(Receipt receipt, CancellationToken ct)
    {
        await EnsureCanManageCustomer(
            receipt.CustomerTaxCode,
            ct,
            "Not allowed to approve this receipt.");
    }

    private async Task<List<AllocationTarget>> LoadTargetsAsync(string sellerTaxCode, string customerTaxCode, CancellationToken ct)
    {
        var invoiceTargets = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.SellerTaxCode == sellerTaxCode && i.CustomerTaxCode == customerTaxCode && i.DeletedAt == null)
            .Where(i => i.OutstandingAmount > 0 && i.Status != "VOID")
            .Select(i => new AllocationTarget(i.Id, AllocationTargetType.Invoice, i.IssueDate, i.OutstandingAmount))
            .ToListAsync(ct);

        var advanceTargets = await _db.Advances
            .AsNoTracking()
            .Where(a => a.SellerTaxCode == sellerTaxCode && a.CustomerTaxCode == customerTaxCode && a.DeletedAt == null)
            .Where(a => a.OutstandingAmount > 0 && (a.Status == "APPROVED" || a.Status == "PAID"))
            .Select(a => new AllocationTarget(a.Id, AllocationTargetType.Advance, a.AdvanceDate, a.OutstandingAmount))
            .ToListAsync(ct);

        invoiceTargets.AddRange(advanceTargets);
        return invoiceTargets;
    }

    private static AllocationMode ParseMode(string mode)
    {
        return mode.ToUpperInvariant() switch
        {
            "BY_INVOICE" => AllocationMode.ByInvoice,
            "BY_PERIOD" => AllocationMode.ByPeriod,
            "FIFO" => AllocationMode.Fifo,
            "PRO_RATA" => AllocationMode.ProRata,
            "MANUAL" => AllocationMode.Manual,
            _ => AllocationMode.Fifo
        };
    }

    public async Task<IReadOnlyList<ReceiptAllocationDetailDto>> ListAllocationsAsync(
        Guid receiptId,
        CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var receipt = await _db.Receipts
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == receiptId && r.DeletedAt == null, ct);

        if (receipt is null)
        {
            throw new InvalidOperationException("Receipt not found.");
        }

        var ownerFilter = _currentUser.ResolveOwnerFilter(
            privilegedRoles: ["Admin", "Supervisor"]);
        if (ownerFilter.HasValue)
        {
            var ownerId = await _db.Customers
                .AsNoTracking()
                .Where(c => c.TaxCode == receipt.CustomerTaxCode)
                .Select(c => c.AccountantOwnerId)
                .FirstOrDefaultAsync(ct);

            if (!ownerId.HasValue || ownerId.Value != ownerFilter.Value)
            {
                throw new UnauthorizedAccessException("Not allowed to view receipt allocations.");
            }
        }

        var invoiceAllocations = await _db.ReceiptAllocations
            .AsNoTracking()
            .Where(a => a.ReceiptId == receiptId && a.InvoiceId != null)
            .Join(_db.Invoices.AsNoTracking().Where(i => i.DeletedAt == null),
                allocation => allocation.InvoiceId!,
                invoice => invoice.Id,
                (allocation, invoice) => new ReceiptAllocationDetailDto(
                    "INVOICE",
                    invoice.Id,
                    invoice.InvoiceNo,
                    invoice.IssueDate,
                    allocation.Amount))
            .ToListAsync(ct);

        var advanceAllocations = await _db.ReceiptAllocations
            .AsNoTracking()
            .Where(a => a.ReceiptId == receiptId && a.AdvanceId != null)
            .Join(_db.Advances.AsNoTracking().Where(a => a.DeletedAt == null),
                allocation => allocation.AdvanceId!,
                advance => advance.Id,
                (allocation, advance) => new ReceiptAllocationDetailDto(
                    "ADVANCE",
                    advance.Id,
                    string.IsNullOrWhiteSpace(advance.AdvanceNo) ? advance.Id.ToString() : advance.AdvanceNo,
                    advance.AdvanceDate,
                    allocation.Amount))
            .ToListAsync(ct);

        return invoiceAllocations.Concat(advanceAllocations).ToList();
    }

    private static IReadOnlyList<AllocationTargetRef>? MapSelectedTargets(IReadOnlyList<ReceiptTargetRef>? selected)
    {
        if (selected is null || selected.Count == 0)
        {
            return null;
        }

        var mapped = new List<AllocationTargetRef>();
        foreach (var item in selected)
        {
            var type = item.TargetType.ToUpperInvariant() switch
            {
                "ADVANCE" => AllocationTargetType.Advance,
                _ => AllocationTargetType.Invoice
            };
            mapped.Add(new AllocationTargetRef(item.Id, type));
        }

        return mapped;
    }

    private async Task ApplyAllocations(Receipt receipt, IReadOnlyList<AllocationLine> lines, CancellationToken ct)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var invoiceIds = lines.Where(l => l.TargetType == AllocationTargetType.Invoice).Select(l => l.TargetId).ToList();
        var advanceIds = lines.Where(l => l.TargetType == AllocationTargetType.Advance).Select(l => l.TargetId).ToList();

        var invoices = await _db.Invoices.Where(i => invoiceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var advances = await _db.Advances.Where(a => advanceIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);

        foreach (var line in lines)
        {
            if (line.TargetType == AllocationTargetType.Invoice && invoices.TryGetValue(line.TargetId, out var invoice))
            {
                invoice.OutstandingAmount = Math.Max(0, invoice.OutstandingAmount - line.Amount);
                invoice.Status = invoice.OutstandingAmount == 0 ? "PAID" : "PARTIAL";
            }

            if (line.TargetType == AllocationTargetType.Advance && advances.TryGetValue(line.TargetId, out var advance))
            {
                advance.OutstandingAmount = Math.Max(0, advance.OutstandingAmount - line.Amount);
                advance.Status = advance.OutstandingAmount == 0 ? "PAID" : "APPROVED";
            }

            _db.ReceiptAllocations.Add(new ReceiptAllocation
            {
                Id = Guid.NewGuid(),
                ReceiptId = receipt.Id,
                TargetType = line.TargetType == AllocationTargetType.Invoice ? "INVOICE" : "ADVANCE",
                InvoiceId = line.TargetType == AllocationTargetType.Invoice ? line.TargetId : null,
                AdvanceId = line.TargetType == AllocationTargetType.Advance ? line.TargetId : null,
                Amount = line.Amount,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

    }

    private static string NormalizeAllocationPriority(string? priority)
    {
        var value = (priority ?? string.Empty).Trim().ToUpperInvariant();
        return value switch
        {
            "DUE_DATE" => value,
            "ISSUE_DATE" => value,
            _ => "ISSUE_DATE"
        };
    }

    private static IReadOnlyList<ReceiptTargetRef> NormalizeSelectedTargets(
        IReadOnlyList<ReceiptTargetRef>? selected)
    {
        if (selected is null || selected.Count == 0)
        {
            return Array.Empty<ReceiptTargetRef>();
        }

        return selected
            .Where(item => item.Id != Guid.Empty)
            .Select(item => new ReceiptTargetRef(item.Id, item.TargetType))
            .ToList();
    }

    private static void ValidateSelectedTargets(
        IReadOnlyList<ReceiptTargetRef> selected,
        IReadOnlyList<ReceiptOpenItemDto> openItems)
    {
        if (selected.Count == 0)
        {
            return;
        }

        var map = openItems.ToDictionary(
            item => (item.TargetType.ToUpperInvariant(), item.TargetId),
            _ => true);

        foreach (var item in selected)
        {
            var key = (item.TargetType.ToUpperInvariant(), item.Id);
            if (!map.ContainsKey(key))
            {
                throw new InvalidOperationException("Danh sách phân bổ có chứng từ không hợp lệ.");
            }
        }
    }

    private static string? SerializeTargets(IReadOnlyList<ReceiptTargetRef>? targets)
    {
        if (targets is null || targets.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(targets);
    }

    private static IReadOnlyList<ReceiptTargetRef>? DeserializeTargets(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<ReceiptTargetRef>>(raw);
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeAllocationMode(string? mode)
    {
        var value = (mode ?? string.Empty).Trim().ToUpperInvariant();
        return value switch
        {
            "BY_INVOICE" => value,
            "BY_PERIOD" => value,
            "FIFO" => value,
            "PRO_RATA" => value,
            "MANUAL" => value,
            _ => throw new InvalidOperationException("Invalid allocation mode.")
        };
    }

    private static string NormalizeMethod(string? method)
    {
        var value = (method ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "BANK";
        }

        return value switch
        {
            "BANK" => value,
            "CASH" => value,
            "OTHER" => value,
            _ => throw new InvalidOperationException("Invalid receipt method.")
        };
    }

    private static string ResolveReceiptApprovalFailureReason(Exception ex)
    {
        return ex switch
        {
            ConcurrencyException => "concurrency",
            UnauthorizedAccessException => "forbidden",
            InvalidOperationException ioex when ioex.Message.Contains("locked", StringComparison.OrdinalIgnoreCase) => "period_locked",
            InvalidOperationException ioex when ioex.Message.Contains("chọn chứng từ", StringComparison.OrdinalIgnoreCase) => "missing_targets",
            InvalidOperationException => "invalid_operation",
            _ => "unknown"
        };
    }
}
