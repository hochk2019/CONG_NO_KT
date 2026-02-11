using Ops.Agent.Services;
using System.ServiceProcess;
using System.Runtime.Versioning;

namespace Ops.Tests;

[SupportedOSPlatform("windows")]
public class ServiceControlTests
{
    [Fact]
    public void NormalizeStatus_MapsRunning()
    {
        var status = ServiceControl.NormalizeStatus(ServiceControllerStatus.Running);
        Assert.Equal("running", status);
    }

    [Fact]
    public void NormalizeStatus_UnknownWhenNull()
    {
        var status = ServiceControl.NormalizeStatus(null);
        Assert.Equal("unknown", status);
    }
}
