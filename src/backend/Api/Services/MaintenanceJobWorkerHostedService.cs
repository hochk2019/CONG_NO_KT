using System.Diagnostics;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Customers;
using CongNoGolden.Application.Maintenance;
using CongNoGolden.Infrastructure.Services.Common;

namespace CongNoGolden.Api.Services;

public sealed class MaintenanceJobWorkerHostedService : BackgroundService
{
    private static readonly string[] CacheNamespaces = ["dashboard", "reports", "risk"];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMaintenanceJobQueue _queue;
    private readonly ILogger<MaintenanceJobWorkerHostedService> _logger;

    public MaintenanceJobWorkerHostedService(
        IServiceScopeFactory scopeFactory,
        IMaintenanceJobQueue queue,
        ILogger<MaintenanceJobWorkerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_queue.TryDequeue(out var item) || item is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                continue;
            }

            _queue.MarkRunning(item.JobId);
            BusinessMetrics.RecordMaintenanceJobStarted(
                item.JobType,
                DateTimeOffset.UtcNow - item.CreatedAtUtc);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var summary = await ProcessJobAsync(scope.ServiceProvider, item, stoppingToken);
                await InvalidateCacheAsync(scope.ServiceProvider, stoppingToken);

                _queue.MarkSucceeded(item.JobId, summary);
                BusinessMetrics.RecordMaintenanceJobCompleted(item.JobType, succeeded: true, stopwatch.Elapsed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _queue.MarkFailed(item.JobId, ex.Message);
                BusinessMetrics.RecordMaintenanceJobCompleted(
                    item.JobType,
                    succeeded: false,
                    duration: stopwatch.Elapsed,
                    failureType: ex.GetType().Name);
                _logger.LogError(ex, "Maintenance job {JobId} failed.", item.JobId);
            }
        }
    }

    private static async Task<string> ProcessJobAsync(
        IServiceProvider serviceProvider,
        MaintenanceJobWorkItem item,
        CancellationToken ct)
    {
        var auditService = serviceProvider.GetRequiredService<IAuditService>();

        return item.JobType switch
        {
            MaintenanceJobType.ReconcileBalances => await RunReconcileAsync(serviceProvider, auditService, item, ct),
            MaintenanceJobType.RunRetention => await RunRetentionAsync(serviceProvider, auditService, item, ct),
            _ => throw new InvalidOperationException($"Unsupported maintenance job type: {item.JobType}.")
        };
    }

    private static async Task<string> RunReconcileAsync(
        IServiceProvider serviceProvider,
        IAuditService auditService,
        MaintenanceJobWorkItem item,
        CancellationToken ct)
    {
        var reconcileService = serviceProvider.GetRequiredService<ICustomerBalanceReconcileService>();
        var request = item.ReconcileRequest ?? new CustomerBalanceReconcileRequest(
            ApplyChanges: true,
            MaxItems: 20,
            Tolerance: 0.01m);

        var result = await reconcileService.ReconcileAsync(request, ct);

        await auditService.LogAsync(
            "CUSTOMER_BALANCE_RECONCILE_ASYNC",
            "Customer",
            "*",
            new
            {
                request.ApplyChanges,
                request.MaxItems,
                request.Tolerance,
                requestedBy = item.RequestedBy
            },
            new
            {
                result.CheckedCustomers,
                result.DriftedCustomers,
                result.UpdatedCustomers,
                result.TotalAbsoluteDrift,
                result.MaxAbsoluteDrift
            },
            ct);

        return $"checked={result.CheckedCustomers};drifted={result.DriftedCustomers};updated={result.UpdatedCustomers}";
    }

    private static async Task<string> RunRetentionAsync(
        IServiceProvider serviceProvider,
        IAuditService auditService,
        MaintenanceJobWorkItem item,
        CancellationToken ct)
    {
        var retentionService = serviceProvider.GetRequiredService<IDataRetentionService>();
        var result = await retentionService.RunAsync(ct);

        await auditService.LogAsync(
            "DATA_RETENTION_RUN_ASYNC",
            "Maintenance",
            "data-retention",
            new
            {
                requestedBy = item.RequestedBy
            },
            new
            {
                result.ExecutedAtUtc,
                result.DeletedAuditLogs,
                result.DeletedImportStagingRows,
                result.DeletedRefreshTokens
            },
            ct);

        return $"audit={result.DeletedAuditLogs};staging={result.DeletedImportStagingRows};refresh={result.DeletedRefreshTokens}";
    }

    private static async Task InvalidateCacheAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        var cache = serviceProvider.GetRequiredService<IReadModelCache>();
        foreach (var namespaceKey in CacheNamespaces)
        {
            await cache.InvalidateNamespaceAsync(namespaceKey, ct);
        }
    }
}
