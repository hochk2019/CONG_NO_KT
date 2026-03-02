namespace CongNoGolden.Application.Backups;

public sealed record BackupRetentionJob(Guid Id, DateTimeOffset FinishedAt);

public static class BackupRetentionPolicy
{
    public static IReadOnlyList<Guid> SelectExpiredJobs(
        IReadOnlyCollection<BackupRetentionJob> jobs,
        int retentionCount)
    {
        if (retentionCount <= 0 || jobs.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        return jobs
            .OrderByDescending(j => j.FinishedAt)
            .Skip(retentionCount)
            .Select(j => j.Id)
            .ToList();
    }
}
