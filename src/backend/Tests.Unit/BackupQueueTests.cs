using System.Reflection;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class BackupQueueTests
{
    [Fact]
    public void Enqueue_Dequeue_ReturnsInOrder()
    {
        var type = Type.GetType("CongNoGolden.Infrastructure.Services.BackupQueue, CongNoGolden.Infrastructure");
        Assert.NotNull(type);

        var queue = Activator.CreateInstance(type!);
        Assert.NotNull(queue);

        var enqueue = type!.GetMethod("Enqueue", BindingFlags.Public | BindingFlags.Instance);
        var tryDequeue = type!.GetMethod("TryDequeue", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(enqueue);
        Assert.NotNull(tryDequeue);

        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        enqueue!.Invoke(queue, new object[] { first });
        enqueue!.Invoke(queue, new object[] { second });

        var args = new object?[] { null };
        var ok1 = (bool)tryDequeue!.Invoke(queue, args)!;
        Assert.True(ok1);
        Assert.Equal(first, args[0]);

        args = new object?[] { null };
        var ok2 = (bool)tryDequeue!.Invoke(queue, args)!;
        Assert.True(ok2);
        Assert.Equal(second, args[0]);
    }

    [Fact]
    public void TryDequeue_WhenEmpty_ReturnsFalse()
    {
        var type = Type.GetType("CongNoGolden.Infrastructure.Services.BackupQueue, CongNoGolden.Infrastructure");
        Assert.NotNull(type);

        var queue = Activator.CreateInstance(type!);
        Assert.NotNull(queue);

        var tryDequeue = type!.GetMethod("TryDequeue", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(tryDequeue);

        var args = new object?[] { null };
        var ok = (bool)tryDequeue!.Invoke(queue, args)!;

        Assert.False(ok);
        Assert.Equal(Guid.Empty, args[0]);
    }
}
