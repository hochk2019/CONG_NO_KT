namespace CongNoGolden.Application.Common.Interfaces;

public interface IMaintenanceState
{
    bool IsActive { get; }
    string? Message { get; }
    void SetActive(bool active, string? message = null);
}
