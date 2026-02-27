using CongNoGolden.Application.Risk;
using CongNoGolden.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Api.Services;

public sealed class RiskModelTrainingHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RiskModelTrainingHostedService> _logger;
    private readonly RiskModelTrainingOptions _options;

    public RiskModelTrainingHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<RiskModelTrainingOptions> options,
        ILogger<RiskModelTrainingHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoRunEnabled)
        {
            _logger.LogInformation("Risk ML scheduler disabled.");
            return;
        }

        var pollMinutes = _options.PollMinutes < 60 ? 60 : _options.PollMinutes;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(pollMinutes));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRiskAiModelService>();
                var result = await service.TrainAsync(
                    new RiskMlTrainRequest(
                        LookbackMonths: _options.LookbackMonths,
                        HorizonDays: _options.HorizonDays,
                        AutoActivate: _options.AutoActivate,
                        MinSamples: _options.MinSamples),
                    stoppingToken);

                _logger.LogInformation(
                    "Risk ML run completed. Status={Status} Samples={Samples} ModelId={ModelId}",
                    result.Run.Status,
                    result.Run.SampleCount,
                    result.Model?.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Risk ML scheduled training failed.");
            }
        }
    }
}

