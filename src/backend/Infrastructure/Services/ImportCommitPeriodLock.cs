using System.Text.Json;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public static class ImportCommitPeriodLock
{
    public static async Task<IReadOnlyList<string>> GetLockedPeriodsAsync(
        ConGNoDbContext db,
        string type,
        List<ImportStagingRow> rows,
        CancellationToken ct)
    {
        var monthKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var quarterKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var yearKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            using var doc = JsonDocument.Parse(row.RawData);
            var raw = doc.RootElement;

            DateOnly? date = type switch
            {
                "INVOICE" => ImportCommitJson.GetDate(raw, "issue_date"),
                "ADVANCE" => ImportCommitJson.GetDate(raw, "advance_date"),
                "RECEIPT" => ImportCommitJson.GetDate(raw, "applied_period_start"),
                _ => null
            };

            if (date is null)
            {
                continue;
            }

            AddDateKeys(date.Value, monthKeys, quarterKeys, yearKeys);
        }

        if (monthKeys.Count == 0 && quarterKeys.Count == 0 && yearKeys.Count == 0)
        {
            return Array.Empty<string>();
        }

        var locked = await db.PeriodLocks
            .AsNoTracking()
            .Where(p =>
                (p.PeriodType == "MONTH" && monthKeys.Contains(p.PeriodKey))
                || (p.PeriodType == "QUARTER" && quarterKeys.Contains(p.PeriodKey))
                || (p.PeriodType == "YEAR" && yearKeys.Contains(p.PeriodKey)))
            .Select(p => new { p.PeriodType, p.PeriodKey })
            .ToListAsync(ct);

        return locked
            .Select(p => $"{p.PeriodType}:{p.PeriodKey}")
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddDateKeys(
        DateOnly date,
        HashSet<string> monthKeys,
        HashSet<string> quarterKeys,
        HashSet<string> yearKeys)
    {
        monthKeys.Add($"{date:yyyy-MM}");
        quarterKeys.Add($"{date:yyyy}-Q{((date.Month - 1) / 3) + 1}");
        yearKeys.Add($"{date:yyyy}");
    }
}
