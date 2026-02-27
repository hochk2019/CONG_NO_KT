using CongNoGolden.Application.Risk;

namespace CongNoGolden.Application.Collections;

public interface ICollectionTaskQueue
{
    CollectionTaskSnapshot Enqueue(EnqueueCollectionTaskRequest request, DateTimeOffset now);
    int EnqueueFromRisk(
        IReadOnlyList<RiskCustomerItem> customers,
        int maxItems,
        decimal minPriorityScore,
        DateTimeOffset now);
    IReadOnlyList<CollectionTaskSnapshot> List(CollectionTaskListRequest request);
    CollectionTaskSnapshot? Get(Guid taskId);
    CollectionTaskSnapshot? Assign(Guid taskId, Guid? assignedTo, DateTimeOffset now);
    CollectionTaskSnapshot? UpdateStatus(Guid taskId, string status, string? note, DateTimeOffset now);
}
