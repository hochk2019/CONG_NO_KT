namespace CongNoGolden.Application.Reports;

public sealed record ReportAgingDistributionDto(
    decimal Bucket0To30,
    decimal Bucket31To60,
    decimal Bucket61To90,
    decimal Bucket91To180,
    decimal BucketOver180
);
