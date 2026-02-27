using CongNoGolden.Domain.Allocation;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public class AllocationEngineTests
{
    [Fact]
    public void ByInvoice_Respects_Selected_Order()
    {
        var targets = new[]
        {
            new AllocationTarget(Guid.NewGuid(), AllocationTargetType.Invoice, new DateOnly(2025, 1, 5), 100),
            new AllocationTarget(Guid.NewGuid(), AllocationTargetType.Invoice, new DateOnly(2025, 1, 10), 50)
        };

        var request = new AllocationRequest(
            120,
            AllocationMode.ByInvoice,
            null,
            new[]
            {
                new AllocationTargetRef(targets[1].Id, targets[1].TargetType),
                new AllocationTargetRef(targets[0].Id, targets[0].TargetType)
            }
        );

        var result = AllocationEngine.Allocate(request, targets);

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(targets[1].Id, result.Lines[0].TargetId);
        Assert.Equal(50, result.Lines[0].Amount);
        Assert.Equal(70, result.Lines[1].Amount);
        Assert.Equal(0, result.UnallocatedAmount);
    }

    [Fact]
    public void ByPeriod_Prioritizes_Applied_Month_Then_Fifo()
    {
        var jan = new AllocationTarget(Guid.NewGuid(), AllocationTargetType.Invoice, new DateOnly(2025, 1, 5), 60);
        var feb = new AllocationTarget(Guid.NewGuid(), AllocationTargetType.Invoice, new DateOnly(2025, 2, 1), 80);
        var mar = new AllocationTarget(Guid.NewGuid(), AllocationTargetType.Invoice, new DateOnly(2025, 3, 1), 40);

        var request = new AllocationRequest(
            100,
            AllocationMode.ByPeriod,
            new DateOnly(2025, 2, 1),
            null
        );

        var result = AllocationEngine.Allocate(request, new[] { mar, jan, feb });

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(feb.Id, result.Lines[0].TargetId);
        Assert.Equal(80, result.Lines[0].Amount);
        Assert.Equal(jan.Id, result.Lines[1].TargetId);
        Assert.Equal(20, result.Lines[1].Amount);
    }

    [Fact]
    public void Fifo_Allocates_Oldest_First()
    {
        var oldItem = new AllocationTarget(Guid.NewGuid(), AllocationTargetType.Invoice, new DateOnly(2024, 12, 15), 30);
        var newItem = new AllocationTarget(Guid.NewGuid(), AllocationTargetType.Invoice, new DateOnly(2025, 1, 2), 30);

        var request = new AllocationRequest(40, AllocationMode.Fifo, null, null);
        var result = AllocationEngine.Allocate(request, new[] { newItem, oldItem });

        Assert.Equal(oldItem.Id, result.Lines[0].TargetId);
        Assert.Equal(30, result.Lines[0].Amount);
        Assert.Equal(newItem.Id, result.Lines[1].TargetId);
        Assert.Equal(10, result.Lines[1].Amount);
    }

    [Fact]
    public void Overpay_Returns_Unallocated()
    {
        var target = new AllocationTarget(Guid.NewGuid(), AllocationTargetType.Advance, new DateOnly(2025, 1, 1), 25);

        var request = new AllocationRequest(40, AllocationMode.Fifo, null, null);
        var result = AllocationEngine.Allocate(request, new[] { target });

        Assert.Single(result.Lines);
        Assert.Equal(25, result.Lines[0].Amount);
        Assert.Equal(15, result.UnallocatedAmount);
    }

    [Fact]
    public void ProRata_Distributes_By_Outstanding_Ratio()
    {
        var first = new AllocationTarget(Guid.NewGuid(), AllocationTargetType.Invoice, new DateOnly(2025, 1, 1), 100);
        var second = new AllocationTarget(Guid.NewGuid(), AllocationTargetType.Invoice, new DateOnly(2025, 1, 2), 300);

        var request = new AllocationRequest(200, AllocationMode.ProRata, null, null);
        var result = AllocationEngine.Allocate(request, new[] { first, second });

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(first.Id, result.Lines[0].TargetId);
        Assert.Equal(50, result.Lines[0].Amount);
        Assert.Equal(second.Id, result.Lines[1].TargetId);
        Assert.Equal(150, result.Lines[1].Amount);
        Assert.Equal(0, result.UnallocatedAmount);
    }

    [Fact]
    public void ProRata_Respects_Selected_Targets_Subset()
    {
        var first = new AllocationTarget(Guid.NewGuid(), AllocationTargetType.Invoice, new DateOnly(2025, 1, 1), 100);
        var second = new AllocationTarget(Guid.NewGuid(), AllocationTargetType.Invoice, new DateOnly(2025, 1, 2), 100);
        var third = new AllocationTarget(Guid.NewGuid(), AllocationTargetType.Invoice, new DateOnly(2025, 1, 3), 100);

        var request = new AllocationRequest(
            90,
            AllocationMode.ProRata,
            null,
            new[]
            {
                new AllocationTargetRef(first.Id, AllocationTargetType.Invoice),
                new AllocationTargetRef(third.Id, AllocationTargetType.Invoice)
            });

        var result = AllocationEngine.Allocate(request, new[] { first, second, third });

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(first.Id, result.Lines[0].TargetId);
        Assert.Equal(45, result.Lines[0].Amount);
        Assert.Equal(third.Id, result.Lines[1].TargetId);
        Assert.Equal(45, result.Lines[1].Amount);
    }
}
