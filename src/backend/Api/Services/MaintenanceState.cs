using CongNoGolden.Application.Common.Interfaces;

namespace CongNoGolden.Api.Services;

public sealed class MaintenanceState : IMaintenanceState
{
    private bool _active;
    private string? _message;

    public bool IsActive => _active;
    public string? Message => _message;

    public void SetActive(bool active, string? message = null)
    {
        _active = active;
        _message = message;
    }
}
