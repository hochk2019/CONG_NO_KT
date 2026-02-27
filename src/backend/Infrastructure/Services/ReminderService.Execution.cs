using System.Text.Json;
using CongNoGolden.Application.Reminders;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReminderService
{
    public async Task<ReminderRunResult> RunAsync(ReminderRunRequest request, CancellationToken ct)
    {
        var force = request.Force;
        var dryRun = request.DryRun;
        var previewLimit = request.PreviewLimit <= 0 ? 50 : Math.Min(request.PreviewLimit, 200);

        if (force)
        {
            _currentUser.EnsureUser();
        }

        var settings = await GetOrCreateSettingsAsync(ct);
        var now = DateTimeOffset.UtcNow;

        if (!force)
        {
            if (!settings.Enabled)
            {
                BusinessMetrics.RecordReminderRun(dryRun, 0, 0, 0, 0);
                return new ReminderRunResult(now, 0, 0, 0, 0, dryRun);
            }

            if (settings.NextRunAt.HasValue && settings.NextRunAt.Value > now)
            {
                BusinessMetrics.RecordReminderRun(dryRun, 0, 0, 0, 0);
                return new ReminderRunResult(now, 0, 0, 0, 0, dryRun);
            }
        }

        var channels = NormalizeStringList(ParseJsonList(settings.Channels, DefaultChannels));
        var levels = NormalizeLevels(ParseJsonList(settings.TargetLevels, DefaultLevels));
        if (channels.Count == 0 || levels.Count == 0)
        {
            BusinessMetrics.RecordReminderRun(dryRun, 0, 0, 0, 0);
            return new ReminderRunResult(now, 0, 0, 0, 0, dryRun);
        }

        var asOf = DateOnly.FromDateTime(now.Date);
        var candidates = await LoadReminderCandidatesAsync(asOf, levels, ct);
        var policyStates = await LoadReminderPolicyStatesAsync(candidates, channels, ct);

        if (dryRun)
        {
            var dryRunOutcome = await EvaluateDryRunOutcomeAsync(
                candidates,
                channels,
                asOf,
                now,
                settings,
                policyStates,
                settings.UpcomingDueDays,
                previewLimit,
                ct);

            BusinessMetrics.RecordReminderRun(
                dryRun: true,
                totalCandidates: dryRunOutcome.TotalCandidates,
                sentCount: dryRunOutcome.Sent,
                failedCount: dryRunOutcome.Failed,
                skippedCount: dryRunOutcome.Skipped);

            return new ReminderRunResult(
                now,
                dryRunOutcome.TotalCandidates,
                dryRunOutcome.Sent,
                dryRunOutcome.Failed,
                dryRunOutcome.Skipped,
                true,
                dryRunOutcome.PreviewItems);
        }

        var sent = 0;
        var failed = 0;
        var skipped = 0;
        var totalCandidates = candidates.Count;
        var escalationTargets = await LoadEscalationTargetsAsync(ct);

        foreach (var candidate in candidates)
        {
            var customerTaxCode = candidate.CustomerTaxCode ?? string.Empty;
            foreach (var channel in channels)
            {
                var policy = EvaluateReminderPolicy(
                    policyStates,
                    customerTaxCode,
                    channel,
                    settings,
                    now);

                if (!policy.ShouldSend)
                {
                    LogOutcome(
                        candidate,
                        channel,
                        "SKIPPED",
                        policy.SkipReason,
                        null,
                        policy.EscalationLevel,
                        policy.SkipReason ?? policy.EscalationReason);
                    skipped++;
                    continue;
                }

                var outcome = await SendReminderAsync(
                    candidate,
                    channel,
                    policy,
                    escalationTargets,
                    ct);

                switch (outcome.Status)
                {
                    case "SENT":
                        sent++;
                        break;
                    case "FAILED":
                        failed++;
                        break;
                    default:
                        skipped++;
                        break;
                }

                if (outcome.CountsAsAttempt)
                {
                    UpdateReminderPolicyState(
                        policyStates,
                        customerTaxCode,
                        channel,
                        policy.EscalationLevel,
                        outcome.Status == "SENT" ? DateTimeOffset.UtcNow : null);
                }
            }
        }

        var upcomingOutcome = await SendUpcomingDueAsync(asOf, settings.UpcomingDueDays, ct);
        totalCandidates += upcomingOutcome.Candidates;
        sent += upcomingOutcome.Sent;
        failed += upcomingOutcome.Failed;
        skipped += upcomingOutcome.Skipped;

        await PersistReminderPolicyStatesAsync(policyStates, ct);

        settings.LastRunAt = now;
        settings.NextRunAt = ResolveNextRun(now, settings.FrequencyDays);
        settings.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        BusinessMetrics.RecordReminderRun(
            dryRun: false,
            totalCandidates: totalCandidates,
            sentCount: sent,
            failedCount: failed,
            skippedCount: skipped);

        return new ReminderRunResult(now, totalCandidates, sent, failed, skipped, false);
    }

    private async Task<IReadOnlyList<ReminderCandidateRow>> LoadReminderCandidatesAsync(
        DateOnly asOf,
        IReadOnlyList<string> levels,
        CancellationToken ct)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        var rows = await connection.QueryAsync<ReminderCandidateRow>(
            new CommandDefinition(ReminderCandidatesSql, new { asOf, levels = levels.ToArray() }, cancellationToken: ct));

        return rows.ToList();
    }

    private async Task<IReadOnlyList<UpcomingDueRow>> LoadUpcomingDueCandidatesAsync(
        DateOnly asOf,
        DateOnly toDate,
        CancellationToken ct)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        var rows = await connection.QueryAsync<UpcomingDueRow>(
            new CommandDefinition(UpcomingDueCandidatesSql, new { asOf, toDate }, cancellationToken: ct));

        return rows.ToList();
    }

    private async Task<ReminderOutcome> SendReminderAsync(
        ReminderCandidateRow candidate,
        string channel,
        ReminderPolicyDecision policy,
        EscalationTargets escalationTargets,
        CancellationToken ct)
    {
        var message = BuildMessage(candidate);
        if (channel == "IN_APP")
        {
            var recipients = new HashSet<Guid>();
            if (candidate.OwnerId.HasValue)
            {
                recipients.Add(candidate.OwnerId.Value);
            }

            if (policy.EscalationLevel >= 2)
            {
                foreach (var supervisorId in escalationTargets.Supervisors)
                {
                    recipients.Add(supervisorId);
                }
            }

            if (policy.EscalationLevel >= 3)
            {
                foreach (var adminId in escalationTargets.Admins)
                {
                    recipients.Add(adminId);
                }
            }

            if (recipients.Count == 0)
            {
                return LogOutcome(
                    candidate,
                    channel,
                    "SKIPPED",
                    "OWNER_MISSING",
                    null,
                    policy.EscalationLevel,
                    policy.EscalationReason);
            }

            var allowed = await FilterRecipientsAsync(recipients, ct);
            if (allowed.Count == 0)
            {
                return LogOutcome(
                    candidate,
                    channel,
                    "SKIPPED",
                    "PREF_DISABLED",
                    null,
                    policy.EscalationLevel,
                    policy.EscalationReason);
            }

            var title = policy.EscalationLevel switch
            {
                3 => "Nhắc rủi ro công nợ (Escalate Admin)",
                2 => "Nhắc rủi ro công nợ (Escalate Supervisor)",
                _ => "Nhắc rủi ro công nợ"
            };

            var metadata = JsonSerializer.Serialize(new
            {
                candidate.CustomerTaxCode,
                candidate.CustomerName,
                candidate.RiskLevel,
                candidate.OverdueAmount,
                candidate.MaxDaysPastDue,
                policy.EscalationLevel,
                policy.EscalationReason
            });

            foreach (var userId in allowed)
            {
                _db.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = title,
                    Body = message,
                    Severity = "WARN",
                    Source = "RISK",
                    Metadata = metadata,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            return LogOutcome(
                candidate,
                channel,
                "SENT",
                null,
                message,
                policy.EscalationLevel,
                policy.EscalationReason);
        }

        if (channel == "ZALO")
        {
            if (candidate.OwnerId is null)
            {
                return LogOutcome(
                    candidate,
                    channel,
                    "SKIPPED",
                    "OWNER_MISSING",
                    null,
                    policy.EscalationLevel,
                    policy.EscalationReason);
            }

            var result = await _zaloClient.SendAsync(candidate.OwnerZaloUserId ?? string.Empty, message, ct);
            if (result.Success)
            {
                return LogOutcome(
                    candidate,
                    channel,
                    "SENT",
                    null,
                    message,
                    policy.EscalationLevel,
                    policy.EscalationReason);
            }

            var status = result.Error == "NOT_CONFIGURED" || result.Error == "MISSING_USER_ID" ? "SKIPPED" : "FAILED";
            return LogOutcome(
                candidate,
                channel,
                status,
                result.Error,
                message,
                policy.EscalationLevel,
                policy.EscalationReason);
        }

        return LogOutcome(
            candidate,
            channel,
            "SKIPPED",
            "UNKNOWN_CHANNEL",
            null,
            policy.EscalationLevel,
            policy.EscalationReason);
    }

    private async Task<UpcomingReminderOutcome> SendUpcomingDueAsync(DateOnly asOf, int upcomingDays, CancellationToken ct)
    {
        if (upcomingDays <= 0)
        {
            return new UpcomingReminderOutcome(0, 0, 0, 0, []);
        }

        var toDate = asOf.AddDays(upcomingDays);
        var rows = await LoadUpcomingDueCandidatesAsync(asOf, toDate, ct);
        if (rows.Count == 0)
        {
            return new UpcomingReminderOutcome(0, 0, 0, 0, []);
        }

        var supervisorIds = await LoadSupervisorIdsAsync(ct);

        var sent = 0;
        var failed = 0;
        var skipped = 0;

        foreach (var row in rows)
        {
            var recipients = new HashSet<Guid>();
            if (row.OwnerId.HasValue)
            {
                recipients.Add(row.OwnerId.Value);
            }

            foreach (var supervisorId in supervisorIds)
            {
                recipients.Add(supervisorId);
            }

            if (recipients.Count == 0)
            {
                skipped++;
                LogUpcomingOutcome(row, "IN_APP", "SKIPPED", "NO_RECIPIENT", null);
                continue;
            }

            var allowed = await FilterRecipientsAsync(recipients, ct);
            if (allowed.Count == 0)
            {
                skipped++;
                LogUpcomingOutcome(row, "IN_APP", "SKIPPED", "PREF_DISABLED", null);
                continue;
            }

            var message = BuildUpcomingDueMessage(row, upcomingDays);
            var metadata = JsonSerializer.Serialize(new
            {
                row.CustomerTaxCode,
                row.CustomerName,
                row.DueAmount,
                row.NearestDueDate,
                row.DocumentCount
            });

            foreach (var userId in allowed)
            {
                _db.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = "Sắp đến hạn thanh toán",
                    Body = message,
                    Severity = "WARN",
                    Source = "RISK",
                    Metadata = metadata,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            LogUpcomingOutcome(row, "IN_APP", "SENT", null, message);
            sent++;
        }

        return new UpcomingReminderOutcome(rows.Count, sent, failed, skipped, []);
    }

    private ReminderOutcome LogOutcome(
        ReminderCandidateRow candidate,
        string channel,
        string status,
        string? error,
        string? message,
        int escalationLevel,
        string? escalationReason)
    {
        var log = new ReminderLog
        {
            Id = Guid.NewGuid(),
            CustomerTaxCode = candidate.CustomerTaxCode ?? string.Empty,
            OwnerUserId = candidate.OwnerId,
            RiskLevel = candidate.RiskLevel ?? "LOW",
            Channel = channel,
            Status = status,
            EscalationLevel = escalationLevel,
            EscalationReason = escalationReason,
            Message = message,
            ErrorDetail = error,
            SentAt = status == "SENT" ? DateTimeOffset.UtcNow : null,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.ReminderLogs.Add(log);

        var countsAsAttempt = status is "SENT" or "FAILED";
        return new ReminderOutcome(status, countsAsAttempt);
    }

    private void LogUpcomingOutcome(
        UpcomingDueRow row,
        string channel,
        string status,
        string? error,
        string? message)
    {
        var log = new ReminderLog
        {
            Id = Guid.NewGuid(),
            CustomerTaxCode = row.CustomerTaxCode ?? string.Empty,
            OwnerUserId = row.OwnerId,
            RiskLevel = "LOW",
            Channel = channel,
            Status = status,
            EscalationLevel = 1,
            EscalationReason = "UPCOMING_DUE",
            Message = message,
            ErrorDetail = error,
            SentAt = status == "SENT" ? DateTimeOffset.UtcNow : null,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.ReminderLogs.Add(log);
    }

    private static string BuildMessage(ReminderCandidateRow candidate)
    {
        var ratio = Math.Round(candidate.OverdueRatio * 100, 1);
        var riskLabel = ResolveRiskLabel(candidate.RiskLevel);
        var overdueAmount = FormatMoney(candidate.OverdueAmount);
        var totalOutstanding = FormatMoney(candidate.TotalOutstanding);

        return string.Join(Environment.NewLine, new[]
        {
            "Cảnh báo công nợ quá hạn",
            $"Khách hàng: {candidate.CustomerName} ({candidate.CustomerTaxCode})",
            $"Nhóm rủi ro: {riskLabel} • Quá hạn tối đa: {candidate.MaxDaysPastDue} ngày",
            $"Tỷ lệ quá hạn: {ratio}% • Giá trị quá hạn: {overdueAmount}",
            $"Tổng dư nợ: {totalOutstanding} • Số lần trễ: {candidate.LateCount}"
        });
    }

    private static string BuildUpcomingDueMessage(UpcomingDueRow row, int upcomingDays)
    {
        var dueAmount = FormatMoney(row.DueAmount);
        var dueDate = row.NearestDueDate.HasValue
            ? row.NearestDueDate.Value.ToString("dd/MM/yyyy")
            : "-";

        return string.Join(Environment.NewLine, new[]
        {
            "Nhắc công nợ sắp đến hạn",
            $"Khách hàng: {row.CustomerName} ({row.CustomerTaxCode})",
            $"Tổng đến hạn trong {upcomingDays} ngày: {dueAmount}",
            $"Hạn gần nhất: {dueDate} • Số chứng từ: {row.DocumentCount}"
        });
    }

    private static string ResolveRiskLabel(string? riskLevel)
    {
        return riskLevel?.Trim().ToUpperInvariant() switch
        {
            "VERY_HIGH" => "Rất cao",
            "HIGH" => "Cao",
            "MEDIUM" => "Trung bình",
            _ => "Thấp"
        };
    }

    private static string FormatMoney(decimal value)
    {
        return string.Format(System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), "{0:N0} đ", value);
    }

    private static ReminderPolicyDecision EvaluateReminderPolicy(
        Dictionary<ReminderPolicyKey, ReminderPolicyState> policyStates,
        string customerTaxCode,
        string channel,
        ReminderSetting settings,
        DateTimeOffset now)
    {
        var key = new ReminderPolicyKey(customerTaxCode, channel);
        var state = policyStates.GetValueOrDefault(key) ?? new ReminderPolicyState();
        var responseStatus = NormalizeResponseStatusForPolicy(state.ResponseStatus);

        if (responseStatus == "RESOLVED")
        {
            return new ReminderPolicyDecision(
                ShouldSend: false,
                EscalationLevel: Math.Max(state.CurrentEscalationLevel, 1),
                EscalationReason: "RESPONSE_RESOLVED",
                SkipReason: "RESPONSE_RESOLVED");
        }

        if (state.AttemptCount >= settings.EscalationMaxAttempts)
        {
            var levelAtLimit = ResolveEscalationLevel(
                state.AttemptCount == 0 ? 1 : state.AttemptCount,
                settings);
            return new ReminderPolicyDecision(
                ShouldSend: false,
                EscalationLevel: levelAtLimit,
                EscalationReason: "MAX_ATTEMPTS_REACHED",
                SkipReason: "MAX_ATTEMPTS_REACHED");
        }

        if (settings.EscalationCooldownHours > 0 &&
            state.LastSentAt.HasValue &&
            state.LastSentAt.Value.AddHours(settings.EscalationCooldownHours) > now)
        {
            var nextAttempt = state.AttemptCount + 1;
            var escalationLevel = ResolveEscalationLevel(nextAttempt, settings);
            return new ReminderPolicyDecision(
                ShouldSend: false,
                EscalationLevel: escalationLevel,
                EscalationReason: "COOLDOWN_ACTIVE",
                SkipReason: "COOLDOWN_ACTIVE");
        }

        var attemptNumber = state.AttemptCount + 1;
        var level = ResolveEscalationLevel(attemptNumber, settings);

        string? reason;
        if (responseStatus == "ACKNOWLEDGED")
        {
            level = 1;
            reason = "ACKNOWLEDGED";
        }
        else if (responseStatus == "DISPUTED")
        {
            level = Math.Max(level, 2);
            reason = level >= 3
                ? "ADMIN_ESCALATION_DISPUTED"
                : "SUPERVISOR_ESCALATION_DISPUTED";
        }
        else if (state.EscalationLocked)
        {
            level = Math.Max(state.CurrentEscalationLevel, 1);
            reason = "ESCALATION_LOCKED";
        }
        else
        {
            reason = level switch
            {
                3 => "ADMIN_ESCALATION",
                2 => "SUPERVISOR_ESCALATION",
                _ => null
            };
        }

        return new ReminderPolicyDecision(
            ShouldSend: true,
            EscalationLevel: level,
            EscalationReason: reason,
            SkipReason: null);
    }

    private static int ResolveEscalationLevel(int attemptNumber, ReminderSetting settings)
    {
        if (attemptNumber >= settings.EscalateToAdminAfter)
        {
            return 3;
        }

        if (attemptNumber >= settings.EscalateToSupervisorAfter)
        {
            return 2;
        }

        return 1;
    }

    private static void UpdateReminderPolicyState(
        Dictionary<ReminderPolicyKey, ReminderPolicyState> policyStates,
        string customerTaxCode,
        string channel,
        int escalationLevel,
        DateTimeOffset? sentAt)
    {
        var key = new ReminderPolicyKey(customerTaxCode, channel);
        if (!policyStates.TryGetValue(key, out var state))
        {
            state = new ReminderPolicyState();
            policyStates[key] = state;
        }

        state.AttemptCount++;
        state.CurrentEscalationLevel = Math.Max(escalationLevel, 1);
        if (sentAt.HasValue)
        {
            state.LastSentAt = sentAt;
        }
    }

    private async Task<Dictionary<ReminderPolicyKey, ReminderPolicyState>> LoadReminderPolicyStatesAsync(
        IReadOnlyList<ReminderCandidateRow> candidates,
        IReadOnlyList<string> channels,
        CancellationToken ct)
    {
        var customerCodes = candidates
            .Select(c => c.CustomerTaxCode ?? string.Empty)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (customerCodes.Length == 0 || channels.Count == 0)
        {
            return new Dictionary<ReminderPolicyKey, ReminderPolicyState>();
        }

        var rows = await _db.ReminderResponseStates
            .AsNoTracking()
            .Where(state => customerCodes.Contains(state.CustomerTaxCode) && channels.Contains(state.Channel))
            .Select(state => new ReminderPolicyStateRow
            {
                CustomerTaxCode = state.CustomerTaxCode,
                Channel = state.Channel,
                AttemptCount = state.AttemptCount,
                LastSentAt = state.LastSentAt,
                CurrentEscalationLevel = state.CurrentEscalationLevel,
                ResponseStatus = state.ResponseStatus,
                LatestResponseAt = state.LatestResponseAt,
                EscalationLocked = state.EscalationLocked
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            keySelector: row => new ReminderPolicyKey(row.CustomerTaxCode, row.Channel),
            elementSelector: row => new ReminderPolicyState
            {
                AttemptCount = row.AttemptCount,
                LastSentAt = row.LastSentAt,
                CurrentEscalationLevel = row.CurrentEscalationLevel,
                ResponseStatus = row.ResponseStatus,
                LatestResponseAt = row.LatestResponseAt,
                EscalationLocked = row.EscalationLocked
            });
    }

    private async Task PersistReminderPolicyStatesAsync(
        Dictionary<ReminderPolicyKey, ReminderPolicyState> policyStates,
        CancellationToken ct)
    {
        if (policyStates.Count == 0)
        {
            return;
        }

        var customerCodes = policyStates.Keys
            .Select(x => x.CustomerTaxCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var channels = policyStates.Keys
            .Select(x => x.Channel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var existing = await _db.ReminderResponseStates
            .Where(x => customerCodes.Contains(x.CustomerTaxCode) && channels.Contains(x.Channel))
            .ToListAsync(ct);

        var lookup = existing.ToDictionary(
            keySelector: x => new ReminderPolicyKey(x.CustomerTaxCode, x.Channel),
            elementSelector: x => x);

        var now = DateTimeOffset.UtcNow;
        foreach (var (key, state) in policyStates)
        {
            if (!lookup.TryGetValue(key, out var entity))
            {
                entity = new ReminderResponseState
                {
                    Id = Guid.NewGuid(),
                    CustomerTaxCode = key.CustomerTaxCode,
                    Channel = key.Channel,
                    ResponseStatus = NormalizeResponseStatusForPolicy(state.ResponseStatus),
                    CreatedAt = now
                };

                _db.ReminderResponseStates.Add(entity);
                lookup[key] = entity;
            }

            entity.ResponseStatus = NormalizeResponseStatusForPolicy(state.ResponseStatus);
            entity.LatestResponseAt = state.LatestResponseAt;
            entity.EscalationLocked = state.EscalationLocked;
            entity.AttemptCount = Math.Max(state.AttemptCount, 0);
            entity.CurrentEscalationLevel = Math.Max(state.CurrentEscalationLevel, 1);
            entity.LastSentAt = state.LastSentAt;
            entity.UpdatedAt = now;
        }
    }

    private static string NormalizeResponseStatusForPolicy(string? responseStatus)
    {
        if (string.IsNullOrWhiteSpace(responseStatus))
        {
            return "NO_RESPONSE";
        }

        var normalized = responseStatus.Trim().ToUpperInvariant();
        if (normalized == "PENDING")
        {
            return "NO_RESPONSE";
        }

        return AllowedResponseStatuses.Contains(normalized)
            ? normalized
            : "NO_RESPONSE";
    }

    private async Task<EscalationTargets> LoadEscalationTargetsAsync(CancellationToken ct)
    {
        var supervisors = await LoadSupervisorIdsAsync(ct);
        var admins = await LoadAdminIdsAsync(ct);
        return new EscalationTargets(supervisors, admins);
    }

    private async Task<IReadOnlyList<Guid>> LoadSupervisorIdsAsync(CancellationToken ct)
    {
        var supervisors = await _db.UserRoles
            .Join(_db.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Code })
            .Where(r => r.Code == "Supervisor")
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync(ct);

        return supervisors;
    }

    private async Task<IReadOnlyList<Guid>> LoadAdminIdsAsync(CancellationToken ct)
    {
        var admins = await _db.UserRoles
            .Join(_db.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Code })
            .Where(r => r.Code == "Admin")
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync(ct);

        return admins;
    }

    private async Task<IReadOnlyList<Guid>> FilterRecipientsAsync(
        IReadOnlySet<Guid> recipients,
        CancellationToken ct)
    {
        if (recipients.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var ids = recipients.ToArray();
        var disabled = await _db.NotificationPreferences
            .AsNoTracking()
            .Where(p => ids.Contains(p.UserId) && !p.ReceiveNotifications)
            .Select(p => p.UserId)
            .ToListAsync(ct);

        return ids.Where(id => !disabled.Contains(id)).ToList();
    }

    private async Task<DryRunOutcome> EvaluateDryRunOutcomeAsync(
        IReadOnlyList<ReminderCandidateRow> candidates,
        IReadOnlyList<string> channels,
        DateOnly asOf,
        DateTimeOffset now,
        ReminderSetting settings,
        Dictionary<ReminderPolicyKey, ReminderPolicyState> policyStates,
        int upcomingDays,
        int previewLimit,
        CancellationToken ct)
    {
        var previewItems = new List<ReminderRunPreviewItem>();
        var sent = 0;
        var failed = 0;
        var skipped = 0;
        var totalCandidates = candidates.Count;
        var escalationTargets = await LoadEscalationTargetsAsync(ct);

        var potentialRecipientIds = candidates
            .Where(c => c.OwnerId.HasValue)
            .Select(c => c.OwnerId!.Value)
            .Concat(escalationTargets.Supervisors)
            .Concat(escalationTargets.Admins)
            .Distinct()
            .ToArray();

        var disabledRecipients = potentialRecipientIds.Length == 0
            ? new HashSet<Guid>()
            : (await _db.NotificationPreferences
                .AsNoTracking()
                .Where(p => potentialRecipientIds.Contains(p.UserId) && !p.ReceiveNotifications)
                .Select(p => p.UserId)
                .ToListAsync(ct))
            .ToHashSet();

        foreach (var candidate in candidates)
        {
            var customerTaxCode = candidate.CustomerTaxCode ?? string.Empty;
            foreach (var channel in channels)
            {
                var policy = EvaluateReminderPolicy(
                    policyStates,
                    customerTaxCode,
                    channel,
                    settings,
                    now);

                var outcome = policy.ShouldSend
                    ? EvaluateCandidateDryRun(candidate, channel, policy, escalationTargets, disabledRecipients)
                    : new ReminderDryRunPlan("SKIPPED", policy.SkipReason);

                switch (outcome.Status)
                {
                    case "SENT":
                        sent += 1;
                        break;
                    case "FAILED":
                        failed += 1;
                        break;
                    default:
                        skipped += 1;
                        break;
                }

                if (outcome.CountsAsAttempt)
                {
                    UpdateReminderPolicyState(
                        policyStates,
                        customerTaxCode,
                        channel,
                        policy.EscalationLevel,
                        outcome.Status == "SENT" ? now : null);
                }

                if (previewItems.Count < previewLimit)
                {
                    var previewReason = outcome.Reason ?? policy.EscalationReason;
                    previewItems.Add(new ReminderRunPreviewItem(
                        candidate.CustomerTaxCode ?? string.Empty,
                        candidate.CustomerName ?? candidate.CustomerTaxCode ?? string.Empty,
                        candidate.OwnerId,
                        candidate.OwnerName,
                        channel,
                        outcome.Status,
                        previewReason));
                }
            }
        }

        var upcoming = await EvaluateUpcomingDryRunOutcomeAsync(asOf, upcomingDays, ct);
        totalCandidates += upcoming.Candidates;
        sent += upcoming.Sent;
        failed += upcoming.Failed;
        skipped += upcoming.Skipped;

        if (previewItems.Count < previewLimit && upcoming.PreviewItems.Count > 0)
        {
            previewItems.AddRange(upcoming.PreviewItems.Take(previewLimit - previewItems.Count));
        }

        return new DryRunOutcome(totalCandidates, sent, failed, skipped, previewItems);
    }

    private ReminderDryRunPlan EvaluateCandidateDryRun(
        ReminderCandidateRow candidate,
        string channel,
        ReminderPolicyDecision policy,
        EscalationTargets escalationTargets,
        IReadOnlySet<Guid> disabledRecipients)
    {
        if (channel == "IN_APP")
        {
            var recipients = new HashSet<Guid>();
            if (candidate.OwnerId.HasValue)
            {
                recipients.Add(candidate.OwnerId.Value);
            }

            if (policy.EscalationLevel >= 2)
            {
                foreach (var supervisorId in escalationTargets.Supervisors)
                {
                    recipients.Add(supervisorId);
                }
            }

            if (policy.EscalationLevel >= 3)
            {
                foreach (var adminId in escalationTargets.Admins)
                {
                    recipients.Add(adminId);
                }
            }

            if (recipients.Count == 0)
            {
                return new ReminderDryRunPlan("SKIPPED", "OWNER_MISSING");
            }

            var hasAllowedRecipient = recipients.Any(id => !disabledRecipients.Contains(id));
            return hasAllowedRecipient
                ? new ReminderDryRunPlan("SENT", null)
                : new ReminderDryRunPlan("SKIPPED", "PREF_DISABLED");
        }

        if (channel == "ZALO")
        {
            if (!candidate.OwnerId.HasValue)
            {
                return new ReminderDryRunPlan("SKIPPED", "OWNER_MISSING");
            }

            return string.IsNullOrWhiteSpace(candidate.OwnerZaloUserId)
                ? new ReminderDryRunPlan("SKIPPED", "MISSING_USER_ID")
                : new ReminderDryRunPlan("SENT", null);
        }

        return new ReminderDryRunPlan("SKIPPED", "UNKNOWN_CHANNEL");
    }

    private async Task<UpcomingReminderOutcome> EvaluateUpcomingDryRunOutcomeAsync(
        DateOnly asOf,
        int upcomingDays,
        CancellationToken ct)
    {
        if (upcomingDays <= 0)
        {
            return new UpcomingReminderOutcome(0, 0, 0, 0, []);
        }

        var toDate = asOf.AddDays(upcomingDays);
        var rows = await LoadUpcomingDueCandidatesAsync(asOf, toDate, ct);
        if (rows.Count == 0)
        {
            return new UpcomingReminderOutcome(0, 0, 0, 0, []);
        }

        var supervisors = await LoadSupervisorIdsAsync(ct);
        var sent = 0;
        var failed = 0;
        var skipped = 0;
        var preview = new List<ReminderRunPreviewItem>();

        foreach (var row in rows)
        {
            var recipients = new HashSet<Guid>();
            if (row.OwnerId.HasValue)
            {
                recipients.Add(row.OwnerId.Value);
            }

            foreach (var supervisor in supervisors)
            {
                recipients.Add(supervisor);
            }

            if (recipients.Count == 0)
            {
                skipped += 1;
                preview.Add(new ReminderRunPreviewItem(
                    row.CustomerTaxCode ?? string.Empty,
                    row.CustomerName ?? row.CustomerTaxCode ?? string.Empty,
                    row.OwnerId,
                    row.OwnerName,
                    "IN_APP",
                    "SKIPPED",
                    "NO_RECIPIENT"));
                continue;
            }

            var allowed = await FilterRecipientsAsync(recipients, ct);
            if (allowed.Count == 0)
            {
                skipped += 1;
                preview.Add(new ReminderRunPreviewItem(
                    row.CustomerTaxCode ?? string.Empty,
                    row.CustomerName ?? row.CustomerTaxCode ?? string.Empty,
                    row.OwnerId,
                    row.OwnerName,
                    "IN_APP",
                    "SKIPPED",
                    "PREF_DISABLED"));
                continue;
            }

            sent += 1;
            preview.Add(new ReminderRunPreviewItem(
                row.CustomerTaxCode ?? string.Empty,
                row.CustomerName ?? row.CustomerTaxCode ?? string.Empty,
                row.OwnerId,
                row.OwnerName,
                "IN_APP",
                "SENT",
                null));
        }

        return new UpcomingReminderOutcome(rows.Count, sent, failed, skipped, preview);
    }

    private sealed record ReminderDryRunPlan(string Status, string? Reason)
    {
        public bool CountsAsAttempt => Status is "SENT" or "FAILED";
    }

    private sealed record DryRunOutcome(
        int TotalCandidates,
        int Sent,
        int Failed,
        int Skipped,
        IReadOnlyList<ReminderRunPreviewItem> PreviewItems);

    private sealed record ReminderOutcome(string Status, bool CountsAsAttempt);

    private sealed record UpcomingReminderOutcome(
        int Candidates,
        int Sent,
        int Failed,
        int Skipped,
        IReadOnlyList<ReminderRunPreviewItem> PreviewItems);

    private readonly record struct ReminderPolicyKey(string CustomerTaxCode, string Channel);

    private sealed class ReminderPolicyState
    {
        public int AttemptCount { get; set; }
        public DateTimeOffset? LastSentAt { get; set; }
        public int CurrentEscalationLevel { get; set; } = 1;
        public string ResponseStatus { get; set; } = "NO_RESPONSE";
        public DateTimeOffset? LatestResponseAt { get; set; }
        public bool EscalationLocked { get; set; }
    }

    private sealed record ReminderPolicyDecision(
        bool ShouldSend,
        int EscalationLevel,
        string? EscalationReason,
        string? SkipReason);

    private sealed record EscalationTargets(
        IReadOnlyList<Guid> Supervisors,
        IReadOnlyList<Guid> Admins);

    private sealed class ReminderCandidateRow
    {
        public string? CustomerTaxCode { get; set; }
        public string? CustomerName { get; set; }
        public Guid? OwnerId { get; set; }
        public string? OwnerName { get; set; }
        public string? OwnerZaloUserId { get; set; }
        public string? RiskLevel { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal OverdueAmount { get; set; }
        public decimal OverdueRatio { get; set; }
        public int MaxDaysPastDue { get; set; }
        public int LateCount { get; set; }
    }

    private sealed class UpcomingDueRow
    {
        public string? CustomerTaxCode { get; set; }
        public string? CustomerName { get; set; }
        public Guid? OwnerId { get; set; }
        public string? OwnerName { get; set; }
        public string? OwnerZaloUserId { get; set; }
        public decimal DueAmount { get; set; }
        public DateOnly? NearestDueDate { get; set; }
        public int DocumentCount { get; set; }
    }

    private sealed class ReminderPolicyStateRow
    {
        public string CustomerTaxCode { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public DateTimeOffset? LastSentAt { get; set; }
        public int CurrentEscalationLevel { get; set; } = 1;
        public string ResponseStatus { get; set; } = "NO_RESPONSE";
        public DateTimeOffset? LatestResponseAt { get; set; }
        public bool EscalationLocked { get; set; }
    }
}
