namespace Ops.Shared.Models;

public sealed record DiagnosticsResponse(
    string BackendServiceName,
    bool BackendServiceExists,
    string BackendExePath,
    bool BackendExeExists,
    string IisSiteName,
    bool IisModuleAvailable,
    bool IisSiteExists,
    string DatabaseHost,
    int DatabasePort,
    bool DatabaseReachable,
    string DatabaseMessage,
    string Notes);
