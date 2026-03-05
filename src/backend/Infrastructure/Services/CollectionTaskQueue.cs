using System.Globalization;
using System.Text;
using CongNoGolden.Application.Collections;
using CongNoGolden.Application.Risk;
using Microsoft.Extensions.Options;

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
    private readonly CollectionTaskScoringOptions _scoring;

    public CollectionTaskQueue()
        : this(Options.Create(new CollectionTaskScoringOptions()))
    {
    }

    public CollectionTaskQueue(IOptions<CollectionTaskScoringOptions> scoringOptions)
    {
        _scoring = NormalizeScoringOptions(scoringOptions?.Value);
    }

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
        var normalizedSearchToken = NormalizeSearchToken(normalizedSearch);

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
                query = query.Where(task => MatchesSearch(task, normalizedSearch, normalizedSearchToken));
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

    private decimal ComputePriorityScore(RiskCustomerItem customer)
    {
        var probability = Clamp01(customer.PredictedOverdueProbability);
        var overdueRatio = Clamp01(customer.OverdueRatio);
        var dayFactor = NormalizeDaysPastDue(customer.MaxDaysPastDue);
        var overdueAmountFactor = Clamp01(customer.OverdueAmount / _scoring.OverdueAmountCap);
        var exposureAmountFactor = Clamp01(customer.TotalOutstanding / _scoring.ExposureAmountCap);
        var expectedValueFactor = Clamp01(probability *
                                          ((overdueAmountFactor * _scoring.ExpectedValueOverdueBlend) +
                                           (exposureAmountFactor * _scoring.ExpectedValueExposureBlend)));
        var levelFactor = ResolveRiskLevelFactor(customer.RiskLevel);

        var score =
            (expectedValueFactor * _scoring.ExpectedValueWeight) +
            (probability * _scoring.ProbabilityWeight) +
            (overdueRatio * _scoring.OverdueRatioWeight) +
            (dayFactor * _scoring.DaysPastDueWeight) +
            (levelFactor * _scoring.RiskLevelWeight);
        return Math.Round(Clamp01(score), 4, MidpointRounding.AwayFromZero);
    }

    private decimal NormalizeDaysPastDue(int value)
    {
        var days = Math.Max(0, value);
        if (days == 0)
        {
            return 0m;
        }

        if (days <= _scoring.DaysBand1End)
        {
            return Interpolate(
                x: days,
                x0: 0,
                x1: _scoring.DaysBand1End,
                y0: 0m,
                y1: _scoring.DaysBand1Score);
        }

        if (days <= _scoring.DaysBand2End)
        {
            return Interpolate(
                x: days,
                x0: _scoring.DaysBand1End,
                x1: _scoring.DaysBand2End,
                y0: _scoring.DaysBand1Score,
                y1: _scoring.DaysBand2Score);
        }

        if (days <= _scoring.DaysBand3End)
        {
            return Interpolate(
                x: days,
                x0: _scoring.DaysBand2End,
                x1: _scoring.DaysBand3End,
                y0: _scoring.DaysBand2Score,
                y1: _scoring.DaysBand3Score);
        }

        if (days <= _scoring.DaysBand4End)
        {
            return Interpolate(
                x: days,
                x0: _scoring.DaysBand3End,
                x1: _scoring.DaysBand4End,
                y0: _scoring.DaysBand3Score,
                y1: _scoring.DaysBand4Score);
        }

        return _scoring.DaysBand4Score;
    }

    private static string NormalizeRiskLevel(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized is "VERY_HIGH" or "HIGH" or "MEDIUM" or "LOW" ? normalized : "LOW";
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

    private decimal ResolveRiskLevelFactor(string? riskLevel)
    {
        return NormalizeRiskLevel(riskLevel).ToUpperInvariant() switch
        {
            "VERY_HIGH" => _scoring.RiskLevelVeryHighFactor,
            "HIGH" => _scoring.RiskLevelHighFactor,
            "MEDIUM" => _scoring.RiskLevelMediumFactor,
            _ => _scoring.RiskLevelLowFactor
        };
    }

    private static decimal Interpolate(int x, int x0, int x1, decimal y0, decimal y1)
    {
        if (x1 <= x0)
        {
            return y1;
        }

        var clampedX = Math.Clamp(x, x0, x1);
        var ratio = (clampedX - x0) / (decimal)(x1 - x0);
        return y0 + ((y1 - y0) * ratio);
    }

    private static CollectionTaskScoringOptions NormalizeScoringOptions(CollectionTaskScoringOptions? options)
    {
        var source = options ?? new CollectionTaskScoringOptions();
        var defaults = new CollectionTaskScoringOptions();

        var overdueAmountCap = source.OverdueAmountCap > 0m ? source.OverdueAmountCap : defaults.OverdueAmountCap;
        var exposureAmountCap = source.ExposureAmountCap > 0m ? source.ExposureAmountCap : defaults.ExposureAmountCap;

        var expectedValueOverdueBlend = source.ExpectedValueOverdueBlend;
        var expectedValueExposureBlend = source.ExpectedValueExposureBlend;
        var blendTotal = Math.Max(expectedValueOverdueBlend, 0m) + Math.Max(expectedValueExposureBlend, 0m);
        if (blendTotal <= 0m)
        {
            expectedValueOverdueBlend = defaults.ExpectedValueOverdueBlend;
            expectedValueExposureBlend = defaults.ExpectedValueExposureBlend;
            blendTotal = expectedValueOverdueBlend + expectedValueExposureBlend;
        }

        expectedValueOverdueBlend = Math.Max(expectedValueOverdueBlend, 0m) / blendTotal;
        expectedValueExposureBlend = Math.Max(expectedValueExposureBlend, 0m) / blendTotal;

        var weightExpectedValue = Math.Max(source.ExpectedValueWeight, 0m);
        var weightProbability = Math.Max(source.ProbabilityWeight, 0m);
        var weightOverdueRatio = Math.Max(source.OverdueRatioWeight, 0m);
        var weightDaysPastDue = Math.Max(source.DaysPastDueWeight, 0m);
        var weightRiskLevel = Math.Max(source.RiskLevelWeight, 0m);
        var weightTotal = weightExpectedValue + weightProbability + weightOverdueRatio + weightDaysPastDue + weightRiskLevel;
        if (weightTotal <= 0m)
        {
            weightExpectedValue = defaults.ExpectedValueWeight;
            weightProbability = defaults.ProbabilityWeight;
            weightOverdueRatio = defaults.OverdueRatioWeight;
            weightDaysPastDue = defaults.DaysPastDueWeight;
            weightRiskLevel = defaults.RiskLevelWeight;
            weightTotal = weightExpectedValue + weightProbability + weightOverdueRatio + weightDaysPastDue + weightRiskLevel;
        }

        weightExpectedValue /= weightTotal;
        weightProbability /= weightTotal;
        weightOverdueRatio /= weightTotal;
        weightDaysPastDue /= weightTotal;
        weightRiskLevel /= weightTotal;

        var daysBand1End = Math.Max(source.DaysBand1End, 1);
        var daysBand2End = Math.Max(source.DaysBand2End, daysBand1End + 1);
        var daysBand3End = Math.Max(source.DaysBand3End, daysBand2End + 1);
        var daysBand4End = Math.Max(source.DaysBand4End, daysBand3End + 1);

        var daysBand1Score = Clamp01(source.DaysBand1Score);
        var daysBand2Score = Math.Max(daysBand1Score, Clamp01(source.DaysBand2Score));
        var daysBand3Score = Math.Max(daysBand2Score, Clamp01(source.DaysBand3Score));
        var daysBand4Score = Math.Max(daysBand3Score, Clamp01(source.DaysBand4Score));

        return new CollectionTaskScoringOptions
        {
            DefaultMinPriorityScore = Clamp01(source.DefaultMinPriorityScore),
            OverdueAmountCap = overdueAmountCap,
            ExposureAmountCap = exposureAmountCap,
            ExpectedValueOverdueBlend = expectedValueOverdueBlend,
            ExpectedValueExposureBlend = expectedValueExposureBlend,
            ExpectedValueWeight = weightExpectedValue,
            ProbabilityWeight = weightProbability,
            OverdueRatioWeight = weightOverdueRatio,
            DaysPastDueWeight = weightDaysPastDue,
            RiskLevelWeight = weightRiskLevel,
            DaysBand1End = daysBand1End,
            DaysBand2End = daysBand2End,
            DaysBand3End = daysBand3End,
            DaysBand4End = daysBand4End,
            DaysBand1Score = daysBand1Score,
            DaysBand2Score = daysBand2Score,
            DaysBand3Score = daysBand3Score,
            DaysBand4Score = daysBand4Score,
            RiskLevelVeryHighFactor = Clamp01(source.RiskLevelVeryHighFactor),
            RiskLevelHighFactor = Clamp01(source.RiskLevelHighFactor),
            RiskLevelMediumFactor = Clamp01(source.RiskLevelMediumFactor),
            RiskLevelLowFactor = Clamp01(source.RiskLevelLowFactor)
        };
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

    private static bool MatchesSearch(MutableTask task, string rawSearch, string normalizedSearchToken)
    {
        if (task.CustomerTaxCode.Contains(rawSearch, StringComparison.OrdinalIgnoreCase) ||
            task.CustomerName.Contains(rawSearch, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrEmpty(normalizedSearchToken))
        {
            return false;
        }

        return NormalizeSearchToken(task.CustomerTaxCode).Contains(normalizedSearchToken, StringComparison.Ordinal) ||
               NormalizeSearchToken(task.CustomerName).Contains(normalizedSearchToken, StringComparison.Ordinal);
    }

    private static string NormalizeSearchToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (ch == 'đ')
            {
                sb.Append('d');
                continue;
            }

            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }
}
