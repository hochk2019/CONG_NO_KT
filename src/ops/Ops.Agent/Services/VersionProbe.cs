using System.Diagnostics;
using Ops.Shared.Models;

namespace Ops.Agent.Services;

public sealed class VersionProbe
{
    public ComponentVersionDto GetComponentVersion(string name, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new ComponentVersionDto(name, null, null, path);

        var info = FileVersionInfo.GetVersionInfo(path);
        var version = string.IsNullOrWhiteSpace(info.FileVersion) ? info.ProductVersion : info.FileVersion;
        var lastWrite = File.GetLastWriteTimeUtc(path);
        return new ComponentVersionDto(name, version, lastWrite, path);
    }
}
