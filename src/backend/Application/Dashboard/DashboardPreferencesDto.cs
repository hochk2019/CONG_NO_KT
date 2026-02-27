namespace CongNoGolden.Application.Dashboard;

public sealed record DashboardPreferencesDto(
    IReadOnlyList<string> WidgetOrder,
    IReadOnlyList<string> HiddenWidgets
);
