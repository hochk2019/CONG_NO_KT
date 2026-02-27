namespace CongNoGolden.Domain.Allocation;

public static class AllocationEngine
{
    public static AllocationResult Allocate(
        AllocationRequest request,
        IReadOnlyList<AllocationTarget> targets)
    {
        if (request.Amount <= 0 || targets.Count == 0)
        {
            return new AllocationResult(Array.Empty<AllocationLine>(), request.Amount);
        }

        var eligible = targets
            .Where(t => t.OutstandingAmount > 0)
            .ToList();

        if (eligible.Count == 0)
        {
            return new AllocationResult(Array.Empty<AllocationLine>(), request.Amount);
        }

        IReadOnlyList<AllocationTarget> ordered = request.Mode switch
        {
            AllocationMode.ByInvoice => OrderBySelected(request, eligible),
            AllocationMode.ByPeriod => OrderByPeriod(request, eligible),
            AllocationMode.Fifo => OrderByFifo(eligible),
            AllocationMode.ProRata => OrderBySelected(request, eligible),
            AllocationMode.Manual => OrderBySelected(request, eligible),
            _ => OrderByFifo(eligible)
        };

        if (request.Mode == AllocationMode.ProRata)
        {
            return AllocateProRata(request.Amount, ordered);
        }

        var lines = new List<AllocationLine>();
        var remaining = request.Amount;

        foreach (var target in ordered)
        {
            if (remaining <= 0)
            {
                break;
            }

            var applied = Math.Min(remaining, target.OutstandingAmount);
            if (applied <= 0)
            {
                continue;
            }

            lines.Add(new AllocationLine(target.Id, target.TargetType, applied));
            remaining -= applied;
        }

        return new AllocationResult(lines.ToArray(), remaining);
    }

    private static IReadOnlyList<AllocationTarget> OrderBySelected(
        AllocationRequest request,
        IReadOnlyList<AllocationTarget> eligible)
    {
        if (request.SelectedTargets is null || request.SelectedTargets.Count == 0)
        {
            return OrderByFifo(eligible);
        }

        var map = eligible.ToDictionary(t => (t.Id, t.TargetType), t => t);
        var ordered = new List<AllocationTarget>();

        foreach (var targetRef in request.SelectedTargets)
        {
            if (map.TryGetValue((targetRef.Id, targetRef.TargetType), out var target))
            {
                ordered.Add(target);
            }
        }

        return ordered.Count == 0 ? OrderByFifo(eligible) : ordered;
    }

    private static IReadOnlyList<AllocationTarget> OrderByPeriod(
        AllocationRequest request,
        IReadOnlyList<AllocationTarget> eligible)
    {
        if (request.AppliedPeriodStart is null)
        {
            return OrderByFifo(eligible);
        }

        var period = request.AppliedPeriodStart.Value;
        var inMonth = eligible
            .Where(t => t.IssueDate.Year == period.Year && t.IssueDate.Month == period.Month)
            .OrderBy(t => t.IssueDate)
            .ToList();

        var others = eligible
            .Except(inMonth)
            .OrderBy(t => t.IssueDate)
            .ToList();

        inMonth.AddRange(others);
        return inMonth;
    }

    private static AllocationResult AllocateProRata(decimal requestedAmount, IReadOnlyList<AllocationTarget> targets)
    {
        if (requestedAmount <= 0 || targets.Count == 0)
        {
            return new AllocationResult(Array.Empty<AllocationLine>(), requestedAmount);
        }

        var totalOutstanding = targets.Sum(t => t.OutstandingAmount);
        if (totalOutstanding <= 0)
        {
            return new AllocationResult(Array.Empty<AllocationLine>(), requestedAmount);
        }

        var allocatable = Math.Min(requestedAmount, totalOutstanding);
        var allocations = new List<ProRataAllocation>(targets.Count);

        foreach (var target in targets)
        {
            var raw = allocatable * (target.OutstandingAmount / totalOutstanding);
            var roundedDown = decimal.Floor(raw * 100m) / 100m;
            var amount = Math.Min(target.OutstandingAmount, roundedDown);
            allocations.Add(new ProRataAllocation(target, amount));
        }

        var distributed = allocations.Sum(a => a.Amount);
        var remainder = allocatable - distributed;
        if (remainder > 0)
        {
            for (var i = 0; i < allocations.Count && remainder > 0; i++)
            {
                var current = allocations[i];
                var capacity = current.Target.OutstandingAmount - current.Amount;
                if (capacity <= 0)
                {
                    continue;
                }

                var delta = Math.Min(capacity, remainder);
                allocations[i] = current with { Amount = current.Amount + delta };
                remainder -= delta;
            }
        }

        var lines = allocations
            .Where(a => a.Amount > 0)
            .Select(a => new AllocationLine(a.Target.Id, a.Target.TargetType, a.Amount))
            .ToArray();

        var totalAllocated = lines.Sum(l => l.Amount);
        return new AllocationResult(lines, requestedAmount - totalAllocated);
    }

    private static IReadOnlyList<AllocationTarget> OrderByFifo(
        IReadOnlyList<AllocationTarget> eligible)
    {
        return eligible.OrderBy(t => t.IssueDate).ToList();
    }

    private readonly record struct ProRataAllocation(AllocationTarget Target, decimal Amount);
}
