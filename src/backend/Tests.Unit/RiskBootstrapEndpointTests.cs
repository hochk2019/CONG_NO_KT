using System.Collections.Concurrent;
using System.Threading;
using CongNoGolden.Api.Endpoints;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.Notifications;
using CongNoGolden.Application.Reminders;
using CongNoGolden.Application.Risk;
using Xunit;

namespace Tests.Unit;

public sealed class RiskBootstrapEndpointTests
{
    [Fact]
    public async Task BuildRiskBootstrapResponseAsync_RunsScopedOperationsSequentially()
    {
        var probe = new ConcurrencyProbe();
        var riskService = new ProbeRiskService(probe);
        var reminderService = new ProbeReminderService(probe);
        var notificationService = new ProbeNotificationService(probe);

        var request = new RiskBootstrapRequest(
            Search: "kh",
            OwnerId: Guid.NewGuid(),
            Level: "HIGH",
            AsOfDate: new DateOnly(2026, 2, 13),
            Page: 1,
            PageSize: 10,
            Sort: "customerName",
            Order: "asc",
            LogChannel: "IN_APP",
            LogStatus: "SENT",
            LogPage: 1,
            LogPageSize: 10,
            NotificationPage: 1,
            NotificationPageSize: 5);

        var response = await RiskEndpoints.BuildRiskBootstrapResponseAsync(
            request,
            riskService,
            reminderService,
            notificationService,
            async ct =>
            {
                using var _ = await probe.EnterAsync(ct);
                await Task.Delay(15, ct);
                return new ZaloLinkStatusResponse(false, null, null);
            },
            CancellationToken.None);

        Assert.False(
            probe.HasConcurrentAccess,
            "Bootstrap execution should not run DbContext-backed operations concurrently.");

        Assert.Equal("HIGH", response.Overview.Items.Single().Level);
        Assert.Single(response.Customers.Items);
        Assert.Single(response.Rules);
        Assert.True(response.Settings.Enabled);
        Assert.Single(response.Logs.Items);
        Assert.Single(response.Notifications.Items);
    }

    private sealed class ProbeRiskService : IRiskService
    {
        private readonly ConcurrencyProbe _probe;

        public ProbeRiskService(ConcurrencyProbe probe)
        {
            _probe = probe;
        }

        public async Task<RiskOverviewDto> GetOverviewAsync(RiskOverviewRequest request, CancellationToken ct)
        {
            using var _ = await _probe.EnterAsync(ct);
            await Task.Delay(15, ct);
            return new RiskOverviewDto(
                request.AsOfDate ?? new DateOnly(2026, 2, 13),
                new[] { new RiskOverviewItem("HIGH", 1, 100m, 50m) },
                1,
                100m,
                50m);
        }

        public async Task<PagedResult<RiskCustomerItem>> ListCustomersAsync(RiskCustomerListRequest request, CancellationToken ct)
        {
            using var _ = await _probe.EnterAsync(ct);
            await Task.Delay(15, ct);
            return new PagedResult<RiskCustomerItem>(
                new[]
                {
                    new RiskCustomerItem(
                        "0312345678",
                        "Cong ty A",
                        request.OwnerId,
                        "Owner A",
                        100m,
                        50m,
                        0.5m,
                        10,
                        2,
                        "HIGH",
                        0.7m,
                        "HIGH",
                        Array.Empty<RiskAiFactorItem>(),
                        "Theo doi sat sao trong 48h.")
                },
                request.Page,
                request.PageSize,
                1);
        }

        public async Task<IReadOnlyList<RiskRuleDto>> GetRulesAsync(CancellationToken ct)
        {
            using var _ = await _probe.EnterAsync(ct);
            await Task.Delay(15, ct);
            return new[] { new RiskRuleDto("HIGH", 10, 0.3m, 2, true, "ANY") };
        }

        public Task<PagedResult<RiskDeltaAlertItem>> ListDeltaAlertsAsync(
            RiskDeltaAlertListRequest request,
            CancellationToken ct)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<RiskScoreHistoryPoint>> GetScoreHistoryAsync(
            string customerTaxCode,
            DateOnly? fromDate,
            DateOnly? toDate,
            int take,
            CancellationToken ct)
            => throw new NotSupportedException();

        public Task UpdateRulesAsync(RiskRulesUpdateRequest request, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<RiskSnapshotCaptureResult> CaptureRiskSnapshotsAsync(
            DateOnly asOfDate,
            decimal absoluteThreshold,
            decimal relativeThresholdRatio,
            CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class ProbeReminderService : IReminderService
    {
        private readonly ConcurrencyProbe _probe;

        public ProbeReminderService(ConcurrencyProbe probe)
        {
            _probe = probe;
        }

        public async Task<ReminderSettingsDto> GetSettingsAsync(CancellationToken ct)
        {
            using var _ = await _probe.EnterAsync(ct);
            await Task.Delay(15, ct);
            return new ReminderSettingsDto(
                true,
                7,
                7,
                3,
                24,
                2,
                3,
                new[] { "IN_APP" },
                new[] { "HIGH" },
                null,
                null);
        }

        public Task UpdateSettingsAsync(ReminderSettingsUpdateRequest request, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<ReminderRunResult> RunAsync(ReminderRunRequest request, CancellationToken ct)
            => throw new NotSupportedException();

        public async Task<PagedResult<ReminderLogItem>> ListLogsAsync(ReminderLogRequest request, CancellationToken ct)
        {
            using var _ = await _probe.EnterAsync(ct);
            await Task.Delay(15, ct);
            return new PagedResult<ReminderLogItem>(
                new[]
                {
                    new ReminderLogItem(
                        Guid.NewGuid(),
                        "0312345678",
                        "Cong ty A",
                        null,
                        null,
                        "HIGH",
                        "IN_APP",
                        "SENT",
                        "ok",
                        null,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow)
                },
                request.Page,
                request.PageSize,
                1);
        }

        public Task<ReminderResponseStateDto?> GetResponseStateAsync(
            string customerTaxCode,
            string channel,
            CancellationToken ct)
            => Task.FromResult<ReminderResponseStateDto?>(null);

        public Task<ReminderResponseStateDto> UpsertResponseStateAsync(
            ReminderResponseStateUpsertRequest request,
            CancellationToken ct)
            => Task.FromResult(new ReminderResponseStateDto(
                request.CustomerTaxCode,
                request.Channel,
                request.ResponseStatus,
                request.ResponseAt,
                request.EscalationLocked ?? false,
                0,
                0,
                null,
                DateTimeOffset.UtcNow));
    }

    private sealed class ProbeNotificationService : INotificationService
    {
        private readonly ConcurrencyProbe _probe;

        public ProbeNotificationService(ConcurrencyProbe probe)
        {
            _probe = probe;
        }

        public async Task<PagedResult<NotificationItem>> ListAsync(NotificationListRequest request, CancellationToken ct)
        {
            using var _ = await _probe.EnterAsync(ct);
            await Task.Delay(15, ct);
            return new PagedResult<NotificationItem>(
                new[]
                {
                    new NotificationItem(
                        Guid.NewGuid(),
                        "Nhac no",
                        "Body",
                        "INFO",
                        "RISK",
                        DateTimeOffset.UtcNow,
                        null)
                },
                request.Page,
                request.PageSize,
                1);
        }

        public Task MarkReadAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();

        public Task<NotificationUnreadCount> GetUnreadCountAsync(CancellationToken ct)
            => throw new NotSupportedException();

        public Task MarkAllReadAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<NotificationPreferencesDto> GetPreferencesAsync(CancellationToken ct)
            => throw new NotSupportedException();

        public Task<NotificationPreferencesDto> UpdatePreferencesAsync(
            NotificationPreferencesUpdate request,
            CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class ConcurrencyProbe
    {
        private int _active;
        private int _maxActive;
        private readonly ConcurrentQueue<string> _trace = new();

        public bool HasConcurrentAccess => Volatile.Read(ref _maxActive) > 1;

        public async Task<IDisposable> EnterAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();

            var active = Interlocked.Increment(ref _active);
            UpdateMax(active);
            _trace.Enqueue($"enter:{active}");
            return new Scope(this);
        }

        private void Exit()
        {
            var active = Interlocked.Decrement(ref _active);
            _trace.Enqueue($"exit:{active}");
        }

        private void UpdateMax(int active)
        {
            while (true)
            {
                var current = Volatile.Read(ref _maxActive);
                if (active <= current)
                {
                    return;
                }

                if (Interlocked.CompareExchange(ref _maxActive, active, current) == current)
                {
                    return;
                }
            }
        }

        private sealed class Scope : IDisposable
        {
            private readonly ConcurrencyProbe _owner;
            private int _disposed;

            public Scope(ConcurrencyProbe owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                {
                    return;
                }

                _owner.Exit();
            }
        }
    }
}
