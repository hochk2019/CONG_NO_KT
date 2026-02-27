namespace CongNoGolden.Application.Dashboard;

public sealed record UpdateDashboardPreferencesRequest(
    IReadOnlyList<string>? WidgetOrder,
    IReadOnlyList<string>? HiddenWidgets
);
