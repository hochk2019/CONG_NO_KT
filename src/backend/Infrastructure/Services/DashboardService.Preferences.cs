using CongNoGolden.Application.Dashboard;
using System.Linq;
using System.Text;
using System.Text.Json;
using Dapper;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class DashboardService
{
    private const string DashboardPreferencesReportKey = "dashboard";

    private static readonly string[] DefaultWidgetOrder =
    [
        "executiveSummary",
        "kpis",
        "cashflow",
        "panels",
        "quickActions"
    ];

    private static readonly JsonSerializerOptions DashboardPreferencesJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string DashboardPreferencesSelectSql = @"
SELECT preferences::text
FROM congno.user_report_preferences
WHERE user_id = @userId
  AND LOWER(report_key) = LOWER(@reportKey);
";

    private const string DashboardPreferencesUpsertSql = @"
INSERT INTO congno.user_report_preferences (user_id, report_key, preferences, created_at, updated_at)
VALUES (@userId, @reportKey, CAST(@preferences AS jsonb), now(), now())
ON CONFLICT (user_id, report_key)
DO UPDATE SET preferences = CAST(@preferences AS jsonb), updated_at = now();
";

    public async Task<DashboardPreferencesDto> GetPreferencesAsync(Guid userId, CancellationToken ct)
    {
        await using var connection = _connectionFactory.CreateRead();
        await connection.OpenAsync(ct);

        var payload = await connection.ExecuteScalarAsync<object?>(
            new CommandDefinition(
                DashboardPreferencesSelectSql,
                new { userId, reportKey = DashboardPreferencesReportKey },
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
            return new DashboardPreferencesDto(DefaultWidgetOrder, Array.Empty<string>());
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

            var parsed = JsonSerializer.Deserialize<DashboardPreferencesPayload>(
                normalizedJson,
                DashboardPreferencesJsonOptions);
            IReadOnlyList<string>? parsedOrder = parsed?.WidgetOrder;
            IReadOnlyList<string>? parsedHidden = parsed?.HiddenWidgets;

            if (parsedOrder is null || parsedHidden is null)
            {
                using var document = JsonDocument.Parse(normalizedJson);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (parsedOrder is null &&
                        TryReadStringArray(document.RootElement, new[] { "widgetOrder", "widget_order" }, out var order))
                    {
                        parsedOrder = order;
                    }

                    if (parsedHidden is null &&
                        TryReadStringArray(document.RootElement, new[] { "hiddenWidgets", "hidden_widgets" }, out var hidden))
                    {
                        parsedHidden = hidden;
                    }
                }
            }

            var normalizedOrder = NormalizeWidgetOrder(parsedOrder ?? []);
            var normalizedHidden = NormalizeHiddenWidgets(parsedHidden ?? []);
            return new DashboardPreferencesDto(normalizedOrder, normalizedHidden);
        }
        catch
        {
            return new DashboardPreferencesDto(DefaultWidgetOrder, Array.Empty<string>());
        }
    }

    public async Task<DashboardPreferencesDto> UpdatePreferencesAsync(
        Guid userId,
        UpdateDashboardPreferencesRequest request,
        CancellationToken ct)
    {
        var current = await GetPreferencesAsync(userId, ct);

        var normalizedOrder = NormalizeWidgetOrder(request.WidgetOrder ?? current.WidgetOrder);
        var normalizedHidden = NormalizeHiddenWidgets(request.HiddenWidgets ?? current.HiddenWidgets);

        var payload = new DashboardPreferencesPayload
        {
            WidgetOrder = normalizedOrder,
            HiddenWidgets = normalizedHidden
        };

        var json = JsonSerializer.Serialize(payload);

        await using var connection = _connectionFactory.CreateWrite();
        await connection.OpenAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(
                DashboardPreferencesUpsertSql,
                new
                {
                    userId,
                    reportKey = DashboardPreferencesReportKey,
                    preferences = json
                },
                cancellationToken: ct));

        return new DashboardPreferencesDto(normalizedOrder, normalizedHidden);
    }

    private static IReadOnlyList<string> NormalizeWidgetOrder(IReadOnlyList<string> items)
    {
        var allowed = new HashSet<string>(DefaultWidgetOrder, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var item in items)
        {
            if (!allowed.Contains(item) || !seen.Add(item))
            {
                continue;
            }

            var canonical = DefaultWidgetOrder.First(
                widget => string.Equals(widget, item, StringComparison.OrdinalIgnoreCase));
            result.Add(canonical);
        }

        foreach (var item in DefaultWidgetOrder)
        {
            if (!seen.Contains(item))
            {
                result.Add(item);
            }
        }

        return result;
    }

    private static IReadOnlyList<string> NormalizeHiddenWidgets(IReadOnlyList<string> items)
    {
        var allowed = new HashSet<string>(DefaultWidgetOrder, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var item in items)
        {
            if (!allowed.Contains(item) || !seen.Add(item))
            {
                continue;
            }

            var canonical = DefaultWidgetOrder.First(
                widget => string.Equals(widget, item, StringComparison.OrdinalIgnoreCase));
            result.Add(canonical);
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

    public sealed class DashboardPreferencesPayload
    {
        public IReadOnlyList<string>? WidgetOrder { get; init; }
        public IReadOnlyList<string>? HiddenWidgets { get; init; }
    }
}
