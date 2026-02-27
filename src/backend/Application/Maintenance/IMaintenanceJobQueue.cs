namespace CongNoGolden.Application.Maintenance;

public interface IMaintenanceJobQueue
{
    MaintenanceJobSnapshot Enqueue(EnqueueMaintenanceJobRequest request);

    bool TryDequeue(out MaintenanceJobWorkItem? item);

    void MarkRunning(Guid jobId);

    void MarkSucceeded(Guid jobId, string summary);

    void MarkFailed(Guid jobId, string error);

    MaintenanceJobSnapshot? Get(Guid jobId);

    IReadOnlyList<MaintenanceJobSnapshot> List(int take);
}
