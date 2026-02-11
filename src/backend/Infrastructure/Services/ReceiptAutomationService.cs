using System.Text.Json;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CongNoGolden.Infrastructure.Services;

public sealed class ReceiptAutomationService : IReceiptAutomationService
{
    private readonly ConGNoDbContext _db;
    private readonly ILogger<ReceiptAutomationService> _logger;

    public ReceiptAutomationService(ConGNoDbContext db, ILogger<ReceiptAutomationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var remindBefore = now.AddDays(-10);

        await AutoAllocateAsync(now, ct);
        await SendRemindersAsync(now, remindBefore, ct);
    }

    private async Task AutoAllocateAsync(DateTimeOffset now, CancellationToken ct)
    {
        var receipts = await _db.Receipts
            .Where(r => r.DeletedAt == null && r.Status == "DRAFT" && r.AllocationStatus == "UNALLOCATED")
            .ToListAsync(ct);

        if (receipts.Count == 0)
        {
            return;
        }

        var supervisorIds = await LoadSupervisorIdsAsync(ct);
        var receiptPairs = receipts
            .Select(r => (r.SellerTaxCode, r.CustomerTaxCode))
            .Distinct()
            .ToList();
        var openItemsLookup = await LoadOpenItemsLookupAsync(receiptPairs, ct);

        foreach (var receipt in receipts)
        {
            try
            {
                var key = (receipt.SellerTaxCode, receipt.CustomerTaxCode);
                if (!openItemsLookup.TryGetValue(key, out var openItems))
                {
                    continue;
                }

                if (openItems.Count == 0)
                {
                    continue;
                }

                var orderedTargets = BuildOrderedTargets(openItems, receipt.AllocationPriority);
                if (orderedTargets.Count == 0)
                {
                    continue;
                }

                receipt.AllocationTargets = JsonSerializer.Serialize(orderedTargets);
                receipt.AllocationStatus = "SUGGESTED";
                receipt.AllocationSource = "AUTO";
                receipt.AllocationSuggestedAt = now;
                receipt.AllocationMode = "MANUAL";
                receipt.UpdatedAt = now;
                receipt.Version += 1;

                if (receipt.ReminderDisabledAt is null)
                {
                    await NotifyAsync(
                        receipt,
                        supervisorIds,
                        "Phiếu thu đã được phân bổ tự động",
                        "Vui lòng kiểm tra và phê duyệt phiếu thu đã được phân bổ tự động.",
                        now,
                        ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-allocate failed for receipt {ReceiptId}", receipt.Id);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SendRemindersAsync(DateTimeOffset now, DateTimeOffset remindBefore, CancellationToken ct)
    {
        var receipts = await _db.Receipts
            .Where(r => r.DeletedAt == null && r.Status == "DRAFT")
            .Where(r => r.ReminderDisabledAt == null)
            .Where(r => r.AllocationStatus == "SUGGESTED" || r.AllocationStatus == "UNALLOCATED")
            .ToListAsync(ct);

        if (receipts.Count == 0)
        {
            return;
        }

        var supervisorIds = await LoadSupervisorIdsAsync(ct);

        foreach (var receipt in receipts)
        {
            var anchor = receipt.AllocationStatus == "SUGGESTED"
                ? receipt.AllocationSuggestedAt ?? receipt.CreatedAt
                : receipt.CreatedAt;

            var last = receipt.LastReminderAt ?? anchor;
            if (last > remindBefore)
            {
                continue;
            }

            var title = receipt.AllocationStatus == "SUGGESTED"
                ? "Phiếu thu chờ duyệt"
                : "Phiếu thu treo chưa phân bổ";
            var body = receipt.AllocationStatus == "SUGGESTED"
                ? "Phiếu thu đã được phân bổ nhưng chưa được duyệt."
                : "Phiếu thu đang treo, chưa có chứng từ để phân bổ.";

            await NotifyAsync(receipt, supervisorIds, title, body, now, ct);
            receipt.LastReminderAt = now;
            receipt.UpdatedAt = now;
            receipt.Version += 1;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task NotifyAsync(
        Receipt receipt,
        IReadOnlyList<Guid> supervisorIds,
        string title,
        string body,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var recipients = new HashSet<Guid>();

        if (receipt.CreatedBy.HasValue)
        {
            recipients.Add(receipt.CreatedBy.Value);
        }

        var ownerId = await _db.Customers
            .AsNoTracking()
            .Where(c => c.TaxCode == receipt.CustomerTaxCode)
            .Select(c => c.AccountantOwnerId)
            .FirstOrDefaultAsync(ct);

        if (ownerId.HasValue)
        {
            recipients.Add(ownerId.Value);
        }

        foreach (var supervisorId in supervisorIds)
        {
            recipients.Add(supervisorId);
        }

        var allowedRecipients = await FilterRecipientsAsync(recipients, ct);

        var metadata = JsonSerializer.Serialize(new
        {
            receiptId = receipt.Id,
            receiptNo = receipt.ReceiptNo,
            status = receipt.Status,
            allocationStatus = receipt.AllocationStatus
        });

        foreach (var userId in allowedRecipients)
        {
            _db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Body = body,
                Severity = "INFO",
                Source = "RECEIPT",
                Metadata = metadata,
                CreatedAt = now
            });
        }

        receipt.LastReminderAt = now;
    }

    private async Task<IReadOnlyList<Guid>> FilterRecipientsAsync(IReadOnlySet<Guid> recipients, CancellationToken ct)
    {
        if (recipients.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var ids = recipients.ToArray();
        var disabled = await _db.NotificationPreferences
            .AsNoTracking()
            .Where(p => ids.Contains(p.UserId) && !p.ReceiveNotifications)
            .Select(p => p.UserId)
            .ToListAsync(ct);

        return ids.Where(id => !disabled.Contains(id)).ToList();
    }

    private async Task<IReadOnlyList<Guid>> LoadSupervisorIdsAsync(CancellationToken ct)
    {
        var supervisors = await _db.UserRoles
            .Join(_db.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Code })
            .Where(r => r.Code == "Supervisor")
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync(ct);

        return supervisors;
    }

    private async Task<Dictionary<(string SellerTaxCode, string CustomerTaxCode), List<ReceiptOpenItemDto>>>
        LoadOpenItemsLookupAsync(
            IReadOnlyCollection<(string SellerTaxCode, string CustomerTaxCode)> pairs,
            CancellationToken ct)
    {
        if (pairs.Count == 0)
        {
            return new Dictionary<(string, string), List<ReceiptOpenItemDto>>();
        }

        var sellerTaxCodes = pairs.Select(pair => pair.SellerTaxCode).Distinct().ToList();
        var customerTaxCodes = pairs.Select(pair => pair.CustomerTaxCode).Distinct().ToList();
        var pairSet = new HashSet<(string SellerTaxCode, string CustomerTaxCode)>(pairs);

        var customers = await _db.Customers
            .AsNoTracking()
            .Where(c => customerTaxCodes.Contains(c.TaxCode))
            .Select(c => new { c.TaxCode, c.PaymentTermsDays })
            .ToListAsync(ct);

        var paymentTermsLookup = customers.ToDictionary(c => c.TaxCode, c => c.PaymentTermsDays);

        var invoiceItems = await _db.Invoices
            .AsNoTracking()
            .Where(i => sellerTaxCodes.Contains(i.SellerTaxCode) && customerTaxCodes.Contains(i.CustomerTaxCode))
            .Where(i => i.DeletedAt == null)
            .Where(i => i.OutstandingAmount > 0 && i.Status != "VOID")
            .ToListAsync(ct);

        var advanceItems = await _db.Advances
            .AsNoTracking()
            .Where(a => sellerTaxCodes.Contains(a.SellerTaxCode) && customerTaxCodes.Contains(a.CustomerTaxCode))
            .Where(a => a.DeletedAt == null)
            .Where(a => a.OutstandingAmount > 0 && (a.Status == "APPROVED" || a.Status == "PAID"))
            .ToListAsync(ct);

        var results = new Dictionary<(string SellerTaxCode, string CustomerTaxCode), List<ReceiptOpenItemDto>>();

        foreach (var invoice in invoiceItems)
        {
            var key = (invoice.SellerTaxCode, invoice.CustomerTaxCode);
            if (!pairSet.Contains(key))
            {
                continue;
            }

            if (!paymentTermsLookup.TryGetValue(invoice.CustomerTaxCode, out var paymentTermsDays))
            {
                continue;
            }

            AddOpenItem(results, key, new ReceiptOpenItemDto(
                "INVOICE",
                invoice.Id,
                invoice.InvoiceNo,
                invoice.IssueDate,
                invoice.IssueDate.AddDays(paymentTermsDays),
                invoice.OutstandingAmount,
                invoice.SellerTaxCode,
                invoice.CustomerTaxCode));
        }

        foreach (var advance in advanceItems)
        {
            var key = (advance.SellerTaxCode, advance.CustomerTaxCode);
            if (!pairSet.Contains(key))
            {
                continue;
            }

            if (!paymentTermsLookup.TryGetValue(advance.CustomerTaxCode, out var paymentTermsDays))
            {
                continue;
            }

            AddOpenItem(results, key, new ReceiptOpenItemDto(
                "ADVANCE",
                advance.Id,
                string.IsNullOrWhiteSpace(advance.AdvanceNo) ? advance.Id.ToString() : advance.AdvanceNo,
                advance.AdvanceDate,
                advance.AdvanceDate.AddDays(paymentTermsDays),
                advance.OutstandingAmount,
                advance.SellerTaxCode,
                advance.CustomerTaxCode));
        }

        return results;
    }

    private static void AddOpenItem(
        Dictionary<(string SellerTaxCode, string CustomerTaxCode), List<ReceiptOpenItemDto>> results,
        (string SellerTaxCode, string CustomerTaxCode) key,
        ReceiptOpenItemDto item)
    {
        if (!results.TryGetValue(key, out var items))
        {
            items = new List<ReceiptOpenItemDto>();
            results[key] = items;
        }

        items.Add(item);
    }

    private static List<ReceiptTargetRef> BuildOrderedTargets(
        IReadOnlyList<ReceiptOpenItemDto> items,
        string? priority)
    {
        var useDueDate = string.Equals(priority, "DUE_DATE", StringComparison.OrdinalIgnoreCase);
        var ordered = items
            .OrderBy(item => useDueDate ? item.DueDate : item.IssueDate)
            .ThenBy(item => item.TargetType == "INVOICE" ? 0 : 1)
            .ThenBy(item => item.DocumentNo ?? string.Empty)
            .ToList();

        return ordered
            .Select(item => new ReceiptTargetRef(item.TargetId, item.TargetType))
            .ToList();
    }
}
