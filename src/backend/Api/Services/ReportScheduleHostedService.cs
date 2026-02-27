using CongNoGolden.Application.Reports;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Api.Services;

public sealed class ReportScheduleWorkerOptions
{
    public bool AutoRunEnabled { get; set; } = true;
    public int PollSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 20;
}

public sealed class ReportScheduleHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReportScheduleHostedService> _logger;
    private readonly ReportScheduleWorkerOptions _options;

    public ReportScheduleHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<ReportScheduleWorkerOptions> options,
        ILogger<ReportScheduleHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoRunEnabled)
        {
            _logger.LogInformation("Report schedule worker disabled.");
            return;
        }

        var pollSeconds = _options.PollSeconds is < 15 or > 3600 ? 60 : _options.PollSeconds;
        var batchSize = _options.BatchSize is < 1 or > 200 ? 20 : _options.BatchSize;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(pollSeconds));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IReportScheduleService>();
                var executed = await service.RunDueSchedulesAsync(batchSize, stoppingToken);
                if (executed > 0)
                {
                    _logger.LogInformation("Report schedule worker executed {Count} due job(s).", executed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Report schedule worker run failed.");
            }
        }
    }
}
