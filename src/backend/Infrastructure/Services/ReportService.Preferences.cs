using System.Text;
using System.Text.Json;
using CongNoGolden.Application.Reports;
using Dapper;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReportService
{
    private const string PreferencesReportKey = "reports";
    private const int DefaultDueSoonDays = 7;
    private const int MinDueSoonDays = 1;
    private const int MaxDueSoonDays = 10;

    private static readonly string[] DefaultKpiOrder =
    [
        "totalOutstanding",
        "outstandingInvoice",
        "outstandingAdvance",
        "unallocatedReceipts",
        "overdueAmount",
        "dueSoonAmount",
        "onTimeCustomers"
    ];

    private static readonly JsonSerializerOptions PreferencesJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

private const string ReportPreferencesSelectSql = @"
SELECT preferences::text
FROM congno.user_report_preferences
WHERE user_id = @userId
  AND LOWER(report_key) = LOWER(@reportKey);
";

    private const string ReportPreferencesUpsertSql = @"
INSERT INTO congno.user_report_preferences (user_id, report_key, preferences, created_at, updated_at)
VALUES (@userId, @reportKey, CAST(@preferences AS jsonb), now(), now())
ON CONFLICT (user_id, report_key)
DO UPDATE SET preferences = CAST(@preferences AS jsonb), updated_at = now();
";

    public async Task<ReportPreferencesDto> GetPreferencesAsync(Guid userId, CancellationToken ct)
    {
        await using var connection = _connectionFactory.CreateRead();
        await connection.OpenAsync(ct);

        var payload = await connection.ExecuteScalarAsync<object?>(
            new CommandDefinition(
                ReportPreferencesSelectSql,
                new { userId, reportKey = PreferencesReportKey },
                cancellationToken: ct));

        var json = payload switch
        {
            null => null,
            string text => text,
            JsonDocument document => document.RootElement.GetRawText(),
            JsonElement element => element.GetRawText(),
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            _ => payload.ToString()
        };

        if (string.IsNullOrWhiteSpace(json))
        {
            return new ReportPreferencesDto(DefaultKpiOrder, DefaultDueSoonDays);
        }

        try
        {
            var normalizedJson = json;
            try
            {
                using var outerDocument = JsonDocument.Parse(json);
                if (outerDocument.RootElement.ValueKind == JsonValueKind.String)
                {
                    var inner = outerDocument.RootElement.GetString();
                    if (!string.IsNullOrWhiteSpace(inner))
                    {
                        normalizedJson = inner;
                    }
                }
            }
            catch
            {
                normalizedJson = json;
            }

            var parsed = JsonSerializer.Deserialize<ReportPreferencesPayload>(normalizedJson, PreferencesJsonOptions);
            IReadOnlyList<string>? parsedOrder = parsed?.KpiOrder;
            var parsedDays = parsed?.DueSoonDays;

            if (parsedOrder is null || parsedDays is null)
            {
                using var document = JsonDocument.Parse(normalizedJson);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (parsedOrder is null &&
                        TryReadStringArray(document.RootElement, new[] { "kpiOrder", "kpi_order" }, out var order))
                    {
                        parsedOrder = order;
                    }

                    if (parsedDays is null &&
                        TryReadInt(document.RootElement, new[] { "dueSoonDays", "due_soon_days" }, out var days))
                    {
                        parsedDays = days;
                    }
                }
            }

            var normalizedOrder = NormalizeKpiOrder(parsedOrder ?? []);
            var normalizedDays = NormalizeDueSoonDays(parsedDays ?? DefaultDueSoonDays);
            return new ReportPreferencesDto(normalizedOrder, normalizedDays);
        }
        catch
        {
            return new ReportPreferencesDto(DefaultKpiOrder, DefaultDueSoonDays);
        }
    }

    public async Task<ReportPreferencesDto> UpdatePreferencesAsync(
        Guid userId,
        UpdateReportPreferencesRequest request,
        CancellationToken ct)
    {
        var current = await GetPreferencesAsync(userId, ct);

        var normalizedOrder = NormalizeKpiOrder(request.KpiOrder ?? current.KpiOrder);
        var normalizedDays = NormalizeDueSoonDays(request.DueSoonDays ?? current.DueSoonDays);

        var payload = new ReportPreferencesPayload
        {
            KpiOrder = normalizedOrder,
            DueSoonDays = normalizedDays
        };
        var json = JsonSerializer.Serialize(payload);
        if (string.Equals(json, "{}", StringComparison.Ordinal))
        {
            json = JsonSerializer.Serialize(new { kpiOrder = normalizedOrder, dueSoonDays = normalizedDays });
        }

        await using var connection = _connectionFactory.CreateWrite();
        await connection.OpenAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(
                ReportPreferencesUpsertSql,
                new
                {
                    userId,
                    reportKey = PreferencesReportKey,
                    preferences = json
                },
                cancellationToken: ct));

        return new ReportPreferencesDto(normalizedOrder, normalizedDays);
    }

    private static int NormalizeDueSoonDays(int value)
    {
        return Math.Clamp(value, MinDueSoonDays, MaxDueSoonDays);
    }

    private static IReadOnlyList<string> NormalizeKpiOrder(IReadOnlyList<string> items)
    {
        var allowed = new HashSet<string>(DefaultKpiOrder, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var item in items)
        {
            if (!allowed.Contains(item) || !seen.Add(item))
            {
                continue;
            }

            var canonical = DefaultKpiOrder.First(kpi => string.Equals(kpi, item, StringComparison.OrdinalIgnoreCase));
            result.Add(canonical);
        }

        foreach (var item in DefaultKpiOrder)
        {
            if (!seen.Contains(item))
            {
                result.Add(item);
            }
        }

        return result;
    }

    private static bool TryReadStringArray(JsonElement root, IReadOnlyList<string> candidates, out IReadOnlyList<string> values)
    {
        foreach (var candidate in candidates)
        {
            if (TryGetPropertyIgnoreCase(root, candidate, out var element) &&
                element.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var value = item.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            list.Add(value);
                        }
                    }
                }

                values = list;
                return true;
            }
        }

        values = Array.Empty<string>();
        return false;
    }

    private static bool TryReadInt(JsonElement root, IReadOnlyList<string> candidates, out int value)
    {
        foreach (var candidate in candidates)
        {
            if (TryGetPropertyIgnoreCase(root, candidate, out var element))
            {
                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
                {
                    return true;
                }

                if (element.ValueKind == JsonValueKind.String &&
                    int.TryParse(element.GetString(), out value))
                {
                    return true;
                }
            }
        }

        value = 0;
        return false;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement root, string name, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    public sealed class ReportPreferencesPayload
    {
        public IReadOnlyList<string>? KpiOrder { get; init; }
        public int? DueSoonDays { get; init; }
    }
}
