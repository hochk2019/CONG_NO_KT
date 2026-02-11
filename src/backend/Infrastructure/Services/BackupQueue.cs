using System.Collections.Concurrent;

namespace CongNoGolden.Infrastructure.Services;

public sealed class BackupQueue
{
    private readonly ConcurrentQueue<Guid> _queue = new();

    public void Enqueue(Guid jobId)
    {
        _queue.Enqueue(jobId);
    }

    public bool TryDequeue(out Guid jobId)
    {
        return _queue.TryDequeue(out jobId);
    }
}
