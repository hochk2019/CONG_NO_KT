using CongNoGolden.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CongNoGolden.Infrastructure.Services;

internal static class DocumentDuplicateGuard
{
    internal const string AdvanceDuplicateMessage =
        "Khoản trả hộ với số chứng từ này đã tồn tại cho người bán và khách hàng này.";

    internal const string ReceiptDuplicateMessage =
        "Phiếu thu với số chứng từ này đã tồn tại cho người bán và khách hàng này.";

    internal static async Task EnsureAdvanceNumberAvailableAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        string advanceNo,
        Guid? excludeAdvanceId,
        CancellationToken ct)
    {
        var query = db.Advances
            .AsNoTracking()
            .Where(a =>
                a.DeletedAt == null &&
                a.SellerTaxCode == sellerTaxCode &&
                a.CustomerTaxCode == customerTaxCode &&
                a.AdvanceNo == advanceNo);

        if (excludeAdvanceId.HasValue)
        {
            query = query.Where(a => a.Id != excludeAdvanceId.Value);
        }

        if (await query.AnyAsync(ct))
        {
            throw new InvalidOperationException(AdvanceDuplicateMessage);
        }
    }

    internal static async Task EnsureReceiptNumberAvailableAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        string receiptNo,
        Guid? excludeReceiptId,
        CancellationToken ct)
    {
        var query = db.Receipts
            .AsNoTracking()
            .Where(r =>
                r.DeletedAt == null &&
                r.SellerTaxCode == sellerTaxCode &&
                r.CustomerTaxCode == customerTaxCode &&
                r.ReceiptNo == receiptNo);

        if (excludeReceiptId.HasValue)
        {
            query = query.Where(r => r.Id != excludeReceiptId.Value);
        }

        if (await query.AnyAsync(ct))
        {
            throw new InvalidOperationException(ReceiptDuplicateMessage);
        }
    }

    internal static Task SaveAdvanceChangesAsync(ConGNoDbContext db, CancellationToken ct)
    {
        return SaveChangesAsync(db, "uq_advances_dedup", AdvanceDuplicateMessage, ct);
    }

    internal static Task SaveReceiptChangesAsync(ConGNoDbContext db, CancellationToken ct)
    {
        return SaveChangesAsync(db, "uq_receipts_dedup", ReceiptDuplicateMessage, ct);
    }

    private static async Task SaveChangesAsync(
        ConGNoDbContext db,
        string constraintName,
        string duplicateMessage,
        CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateConstraint(ex, constraintName))
        {
            throw new InvalidOperationException(duplicateMessage, ex);
        }
    }

    private static bool IsDuplicateConstraint(DbUpdateException exception, string constraintName)
    {
        return exception.InnerException is PostgresException postgresException &&
               string.Equals(postgresException.ConstraintName, constraintName, StringComparison.Ordinal);
    }
}
