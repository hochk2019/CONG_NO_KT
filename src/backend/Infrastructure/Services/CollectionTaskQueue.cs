using CongNoGolden.Application.Collections;
using CongNoGolden.Application.Risk;

namespace CongNoGolden.Infrastructure.Services;

public sealed class CollectionTaskQueue : ICollectionTaskQueue
{
    private sealed class MutableTask
    {
        public required Guid TaskId { get; init; }
        public required string CustomerTaxCode { get; init; }
        public required string CustomerName { get; init; }
        public Guid? OwnerId { get; init; }
        public string? OwnerName { get; init; }
        public decimal TotalOutstanding { get; init; }
        public decimal OverdueAmount { get; init; }
        public int MaxDaysPastDue { get; init; }
        public decimal PredictedOverdueProbability { get; init; }
        public required string RiskLevel { get; init; }
        public required string AiSignal { get; init; }
        public decimal PriorityScore { get; init; }
        public required string Status { get; set; }
        public Guid? AssignedTo { get; set; }
        public string? Note { get; set; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
    }

    private readonly object _sync = new();
    private readonly Dictionary<Guid, MutableTask> _tasks = new();

    public CollectionTaskSnapshot Enqueue(EnqueueCollectionTaskRequest request, DateTimeOffset now)
    {
        var taxCode = (request.CustomerTaxCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(taxCode))
        {
            throw new InvalidOperationException("Customer tax code is required.");
        }

        var name = (request.CustomerName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Customer name is required.");
        }

        var riskLevel = NormalizeRiskLevel(request.RiskLevel);
        var aiSignal = NormalizeAiSignal(request.AiSignal);
        var priorityScore = Clamp01(request.PriorityScore);

        lock (_sync)
        {
            var existingOpen = _tasks.Values.FirstOrDefault(task =>
                task.CustomerTaxCode == taxCode &&
                task.Status is CollectionTaskStatusCodes.Open or CollectionTaskStatusCodes.InProgress);
            if (existingOpen is not null)
            {
                return ToSnapshot(existingOpen);
            }

            var task = new MutableTask
            {
                TaskId = Guid.NewGuid(),
                CustomerTaxCode = taxCode,
                CustomerName = name,
                OwnerId = request.OwnerId,
                OwnerName = request.OwnerName?.Trim(),
                TotalOutstanding = request.TotalOutstanding,
                OverdueAmount = request.OverdueAmount,
                MaxDaysPastDue = request.MaxDaysPastDue,
                PredictedOverdueProbability = Clamp01(request.PredictedOverdueProbability),
                RiskLevel = riskLevel,
                AiSignal = aiSignal,
                PriorityScore = priorityScore,
                Status = CollectionTaskStatusCodes.Open,
                AssignedTo = null,
                Note = null,
                CreatedAt = now,
                UpdatedAt = now,
                CompletedAt = null
            };

            _tasks[task.TaskId] = task;
            return ToSnapshot(task);
        }
    }

    public int EnqueueFromRisk(
        IReadOnlyList<RiskCustomerItem> customers,
        int maxItems,
        decimal minPriorityScore,
        DateTimeOffset now)
    {
        if (customers.Count == 0)
        {
            return 0;
        }

        var normalizedTake = Math.Clamp(maxItems, 1, 500);
        var normalizedMinScore = Clamp01(minPriorityScore);
        var candidates = customers
            .Select(customer => new
            {
                Customer = customer,
                Score = ComputePriorityScore(customer)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Customer.CustomerTaxCode))
            .Where(item => item.Score >= normalizedMinScore)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Customer.OverdueAmount)
            .ThenBy(item => item.Customer.CustomerTaxCode, StringComparer.Ordinal)
            .GroupBy(item => item.Customer.CustomerTaxCode.Trim().ToUpperInvariant(), StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(normalizedTake)
            .ToList();

        var created = 0;
        var createdTaskIds = new HashSet<Guid>();
        foreach (var item in candidates)
        {
            var snapshot = Enqueue(
                new EnqueueCollectionTaskRequest(
                    item.Customer.CustomerTaxCode,
                    item.Customer.CustomerName,
                    item.Customer.OwnerId,
                    item.Customer.OwnerName,
                    item.Customer.TotalOutstanding,
                    item.Customer.OverdueAmount,
                    item.Customer.MaxDaysPastDue,
                    item.Customer.PredictedOverdueProbability,
                    item.Customer.RiskLevel,
                    item.Customer.AiSignal,
                    item.Score),
                now);

            if (snapshot.CreatedAt == now && createdTaskIds.Add(snapshot.TaskId))
            {
                created += 1;
            }
        }

        return created;
    }

    public IReadOnlyList<CollectionTaskSnapshot> List(CollectionTaskListRequest request)
    {
        var normalizedTake = Math.Clamp(request.Take, 1, 500);
        var normalizedStatus = NormalizeOptionalStatus(request.Status);
        var normalizedSearch = (request.Search ?? string.Empty).Trim();

        lock (_sync)
        {
            var query = _tasks.Values.AsEnumerable();
            if (normalizedStatus is not null)
            {
                query = query.Where(task => task.Status == normalizedStatus);
            }

            if (request.AssignedTo is not null)
            {
                query = query.Where(task => task.AssignedTo == request.AssignedTo);
            }

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                var keyword = normalizedSearch.ToUpperInvariant();
                query = query.Where(task =>
                    task.CustomerTaxCode.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    task.CustomerName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
            }

            return query
                .OrderByDescending(task => task.PriorityScore)
                .ThenByDescending(task => task.OverdueAmount)
                .ThenByDescending(task => task.CreatedAt)
                .Take(normalizedTake)
                .Select(ToSnapshot)
                .ToList();
        }
    }

    public CollectionTaskSnapshot? Get(Guid taskId)
    {
        lock (_sync)
        {
            return _tasks.TryGetValue(taskId, out var task) ? ToSnapshot(task) : null;
        }
    }

    public CollectionTaskSnapshot? Assign(Guid taskId, Guid? assignedTo, DateTimeOffset now)
    {
        lock (_sync)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
            {
                return null;
            }

            task.AssignedTo = assignedTo;
            task.UpdatedAt = now;
            return ToSnapshot(task);
        }
    }

    public CollectionTaskSnapshot? UpdateStatus(Guid taskId, string status, string? note, DateTimeOffset now)
    {
        var normalizedStatus = NormalizeStatus(status);

        lock (_sync)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
            {
                return null;
            }

            task.Status = normalizedStatus;
            task.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            task.UpdatedAt = now;
            task.CompletedAt = normalizedStatus is CollectionTaskStatusCodes.Done or CollectionTaskStatusCodes.Cancelled
                ? now
                : null;
            return ToSnapshot(task);
        }
    }

    private static CollectionTaskSnapshot ToSnapshot(MutableTask task) =>
        new(
            task.TaskId,
            task.CustomerTaxCode,
            task.CustomerName,
            task.OwnerId,
            task.OwnerName,
            task.TotalOutstanding,
            task.OverdueAmount,
            task.MaxDaysPastDue,
            task.PredictedOverdueProbability,
            task.RiskLevel,
            task.AiSignal,
            task.PriorityScore,
            task.Status,
            task.AssignedTo,
            task.Note,
            task.CreatedAt,
            task.UpdatedAt,
            task.CompletedAt);

    private static decimal ComputePriorityScore(RiskCustomerItem customer)
    {
        var probability = Clamp01(customer.PredictedOverdueProbability);
        var overdueRatio = Clamp01(customer.OverdueRatio);
        var dayFactor = Clamp01(customer.MaxDaysPastDue / 180m);
        var overdueAmountFactor = Clamp01(customer.OverdueAmount / 500_000_000m);
        var exposureAmountFactor = Clamp01(customer.TotalOutstanding / 800_000_000m);
        var expectedValueFactor = Clamp01(probability * ((overdueAmountFactor * 0.70m) + (exposureAmountFactor * 0.30m)));
        var levelFactor = customer.RiskLevel.ToUpperInvariant() switch
        {
            "HIGH" => 1m,
            "MEDIUM" => 0.65m,
            _ => 0.25m
        };

        var score =
            (expectedValueFactor * 0.55m) +
            (probability * 0.20m) +
            (overdueRatio * 0.10m) +
            (dayFactor * 0.10m) +
            (levelFactor * 0.05m);
        return Math.Round(Clamp01(score), 4, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeRiskLevel(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized is "HIGH" or "MEDIUM" or "LOW" ? normalized : "LOW";
    }

    private static string NormalizeAiSignal(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized is "HIGH" or "MEDIUM" or "LOW" ? normalized : "LOW";
    }

    private static decimal Clamp01(decimal value)
    {
        if (value < 0m)
        {
            return 0m;
        }

        if (value > 1m)
        {
            return 1m;
        }

        return value;
    }

    private static string? NormalizeOptionalStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NormalizeStatus(value);
    }

    private static string NormalizeStatus(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (!CollectionTaskStatusCodes.IsValid(normalized))
        {
            throw new InvalidOperationException("Invalid collection task status.");
        }

        return normalized;
    }
}
