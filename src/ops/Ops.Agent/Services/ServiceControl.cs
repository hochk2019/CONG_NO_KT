using System.ServiceProcess;
using System.Runtime.Versioning;
using Ops.Shared.Models;

namespace Ops.Agent.Services;

[SupportedOSPlatform("windows")]
public sealed class ServiceControl
{
    public static string NormalizeStatus(ServiceControllerStatus? status) => status switch
    {
        ServiceControllerStatus.Running => "running",
        ServiceControllerStatus.Stopped => "stopped",
        ServiceControllerStatus.Paused => "paused",
        ServiceControllerStatus.StartPending => "starting",
        ServiceControllerStatus.StopPending => "stopping",
        _ => "unknown"
    };

    public bool Exists(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            _ = sc.Status;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public ServiceStatusDto GetStatus(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            return new ServiceStatusDto(serviceName, NormalizeStatus(sc.Status));
        }
        catch (Exception ex)
        {
            return new ServiceStatusDto(serviceName, "missing", ex.Message);
        }
    }

    public ServiceStatusDto Start(string serviceName)
    {
        if (!Exists(serviceName))
            return new ServiceStatusDto(serviceName, "missing", "Service not found");

        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Running)
                return new ServiceStatusDto(serviceName, "running");

            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            return new ServiceStatusDto(serviceName, NormalizeStatus(sc.Status));
        }
        catch (Exception ex)
        {
            return new ServiceStatusDto(serviceName, "error", ex.Message);
        }
    }

    public ServiceStatusDto Stop(string serviceName)
    {
        if (!Exists(serviceName))
            return new ServiceStatusDto(serviceName, "missing", "Service not found");

        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Stopped)
                return new ServiceStatusDto(serviceName, "stopped");

            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            return new ServiceStatusDto(serviceName, NormalizeStatus(sc.Status));
        }
        catch (Exception ex)
        {
            return new ServiceStatusDto(serviceName, "error", ex.Message);
        }
    }

    public ServiceStatusDto Restart(string serviceName)
    {
        if (!Exists(serviceName))
            return new ServiceStatusDto(serviceName, "missing", "Service not found");

        Stop(serviceName);
        return Start(serviceName);
    }
}
