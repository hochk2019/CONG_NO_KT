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
            AllocationMode.Manual => OrderBySelected(request, eligible),
            _ => OrderByFifo(eligible)
        };

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

    private static IReadOnlyList<AllocationTarget> OrderByFifo(
        IReadOnlyList<AllocationTarget> eligible)
    {
        return eligible.OrderBy(t => t.IssueDate).ToList();
    }
}
