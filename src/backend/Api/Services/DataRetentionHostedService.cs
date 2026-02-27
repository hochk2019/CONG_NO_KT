using CongNoGolden.Application.Maintenance;
using CongNoGolden.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Api.Services;

public sealed class DataRetentionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataRetentionHostedService> _logger;
    private readonly DataRetentionOptions _options;

    public DataRetentionHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<DataRetentionOptions> options,
        ILogger<DataRetentionHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoRunEnabled)
        {
            _logger.LogInformation("Data retention scheduler disabled.");
            return;
        }

        var pollMinutes = _options.PollMinutes < 60 ? 60 : _options.PollMinutes;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(pollMinutes));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var retentionService = scope.ServiceProvider.GetRequiredService<IDataRetentionService>();
                var result = await retentionService.RunAsync(stoppingToken);
                _logger.LogInformation(
                    "Data retention run done at {ExecutedAtUtc}. Deleted audit={Audit}, staging={Staging}, refreshTokens={Refresh}",
                    result.ExecutedAtUtc,
                    result.DeletedAuditLogs,
                    result.DeletedImportStagingRows,
                    result.DeletedRefreshTokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data retention run failed.");
            }
        }
    }
}
