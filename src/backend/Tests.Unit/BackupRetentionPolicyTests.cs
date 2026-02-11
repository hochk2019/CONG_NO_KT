using System.Collections;
using System.Reflection;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class BackupRetentionPolicyTests
{
    [Fact]
    public void SelectExpiredJobs_ReturnsOldestBeyondRetention()
    {
        var policyType = Type.GetType("CongNoGolden.Application.Backups.BackupRetentionPolicy, CongNoGolden.Application");
        var jobType = Type.GetType("CongNoGolden.Application.Backups.BackupRetentionJob, CongNoGolden.Application");
        Assert.NotNull(policyType);
        Assert.NotNull(jobType);

        var ctor = jobType!.GetConstructor(new[] { typeof(Guid), typeof(DateTimeOffset) });
        Assert.NotNull(ctor);

        var listType = typeof(List<>).MakeGenericType(jobType);
        var list = (IList)Activator.CreateInstance(listType)!;

        var newest = Guid.NewGuid();
        var mid = Guid.NewGuid();
        var oldest = Guid.NewGuid();

        list.Add(ctor!.Invoke(new object[] { newest, new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero) }));
        list.Add(ctor!.Invoke(new object[] { mid, new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero) }));
        list.Add(ctor!.Invoke(new object[] { oldest, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) }));

        var method = policyType!.GetMethod("SelectExpiredJobs", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, new object[] { list, 2 }) as IReadOnlyList<Guid>;
        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal(oldest, result![0]);
    }
}
