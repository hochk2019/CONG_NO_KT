using System;

namespace Ops.Shared.Models;

public sealed record SystemMetricsDto(
    double CpuUsagePercent,
    double MemoryUsedMb,
    double MemoryTotalMb,
    double DiskFreeGb,
    double DiskTotalGb,
    DateTimeOffset SampledAt);
