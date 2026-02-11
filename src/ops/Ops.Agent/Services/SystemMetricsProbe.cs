using System.Diagnostics;
using System.Runtime.InteropServices;
using Ops.Shared.Models;

namespace Ops.Agent.Services;

public sealed class SystemMetricsProbe
{
    private readonly PerformanceCounter _cpuCounter = new("Processor", "% Processor Time", "_Total");
    private static readonly uint MemoryStatusSize = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));

    public async Task<SystemMetricsDto> GetAsync(string? samplePath, CancellationToken ct)
    {
        var cpu = await GetCpuUsageAsync(ct);
        var (usedMb, totalMb) = GetMemoryUsage();
        var (freeGb, totalGb) = GetDiskUsage(samplePath);
        return new SystemMetricsDto(cpu, usedMb, totalMb, freeGb, totalGb, DateTimeOffset.Now);
    }

    private async Task<double> GetCpuUsageAsync(CancellationToken ct)
    {
        _cpuCounter.NextValue();
        await Task.Delay(250, ct);
        return Math.Round(_cpuCounter.NextValue(), 2);
    }

    private static (double usedMb, double totalMb) GetMemoryUsage()
    {
        try
        {
            var status = new MemoryStatusEx { dwLength = MemoryStatusSize };
            if (!GlobalMemoryStatusEx(status))
                return (0, 0);

            var totalMb = status.ullTotalPhys / 1024d / 1024d;
            var availableMb = status.ullAvailPhys / 1024d / 1024d;
            var usedMb = Math.Max(totalMb - availableMb, 0);
            return (Math.Round(usedMb, 2), Math.Round(totalMb, 2));
        }
        catch
        {
            return (0, 0);
        }
    }

    private static (double freeGb, double totalGb) GetDiskUsage(string? samplePath)
    {
        try
        {
            var root = string.IsNullOrWhiteSpace(samplePath)
                ? Path.GetPathRoot(Environment.SystemDirectory)
                : Path.GetPathRoot(samplePath);
            if (string.IsNullOrWhiteSpace(root))
                root = Path.GetPathRoot(Environment.SystemDirectory);

            var drive = DriveInfo.GetDrives().FirstOrDefault(d =>
                string.Equals(d.Name, root, StringComparison.OrdinalIgnoreCase));

            if (drive is null || !drive.IsReady)
                return (0, 0);

            var totalGb = drive.TotalSize / 1024d / 1024d / 1024d;
            var freeGb = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
            return (Math.Round(freeGb, 2), Math.Round(totalGb, 2));
        }
        catch
        {
            return (0, 0);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}
