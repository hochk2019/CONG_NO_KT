using System.Text.Json;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReceiptService
{
    private async Task NotifyPartialAllocationAsync(Receipt receipt, CancellationToken ct)
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

        var supervisorIds = await LoadSupervisorIdsAsync(ct);
        foreach (var supervisorId in supervisorIds)
        {
            recipients.Add(supervisorId);
        }

        if (recipients.Count == 0)
        {
            return;
        }
        var allowedRecipients = await FilterRecipientsAsync(recipients, ct);
        if (allowedRecipients.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var title = "Phiếu thu phân bổ một phần";
        var receiptNo = string.IsNullOrWhiteSpace(receipt.ReceiptNo)
            ? receipt.Id.ToString()
            : receipt.ReceiptNo;
        var unallocated = FormatMoney(receipt.UnallocatedAmount);
        var body = $"Phiếu thu {receiptNo} còn {unallocated} chưa phân bổ.";

        var metadata = JsonSerializer.Serialize(new
        {
            receiptId = receipt.Id,
            receiptNo = receipt.ReceiptNo,
            status = receipt.Status,
            allocationStatus = receipt.AllocationStatus,
            unallocatedAmount = receipt.UnallocatedAmount,
            customerTaxCode = receipt.CustomerTaxCode
        });

        foreach (var userId in allowedRecipients)
        {
            _db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Body = body,
                Severity = "WARN",
                Source = "RECEIPT",
                Metadata = metadata,
                CreatedAt = now
            });
        }
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
        return await _db.UserRoles
            .Join(_db.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Code })
            .Where(r => r.Code == "Supervisor")
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync(ct);
    }

    private static string FormatMoney(decimal value)
    {
        return string.Format(System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), "{0:N0} đ", value);
    }
}
