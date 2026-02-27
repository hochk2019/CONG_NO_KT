using CongNoGolden.Application.Risk;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Api.Services;

public sealed class RiskDeltaWorkerOptions
{
    public bool AutoRunEnabled { get; set; } = true;
    public int PollMinutes { get; set; } = 360;
    public decimal AbsoluteThreshold { get; set; } = 0.15m;
    public decimal RelativeThresholdRatio { get; set; } = 0.25m;
}

public sealed class RiskDeltaHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RiskDeltaHostedService> _logger;
    private readonly RiskDeltaWorkerOptions _options;

    public RiskDeltaHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<RiskDeltaWorkerOptions> options,
        ILogger<RiskDeltaHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoRunEnabled)
        {
            _logger.LogInformation("Risk delta worker disabled.");
            return;
        }

        var pollMinutes = _options.PollMinutes is < 30 or > 1440 ? 360 : _options.PollMinutes;
        var absoluteThreshold = _options.AbsoluteThreshold < 0m ? 0m : _options.AbsoluteThreshold;
        var relativeThreshold = _options.RelativeThresholdRatio < 0m ? 0m : _options.RelativeThresholdRatio;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(pollMinutes));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRiskService>();
                var asOfDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
                var result = await service.CaptureRiskSnapshotsAsync(
                    asOfDate,
                    absoluteThreshold,
                    relativeThreshold,
                    stoppingToken);

                if (result.SnapshotCount > 0 || result.AlertCount > 0)
                {
                    _logger.LogInformation(
                        "Risk delta worker captured {Snapshots} snapshot(s), {Alerts} alert(s), {Notifications} notification(s) for {AsOfDate}.",
                        result.SnapshotCount,
                        result.AlertCount,
                        result.NotificationCount,
                        result.AsOfDate);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Risk delta worker run failed.");
            }
        }
    }
}
