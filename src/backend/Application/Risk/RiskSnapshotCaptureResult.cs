namespace CongNoGolden.Application.Risk;

public sealed record RiskSnapshotCaptureResult(
    DateOnly AsOfDate,
    int SnapshotCount,
    int AlertCount,
    int NotificationCount);
