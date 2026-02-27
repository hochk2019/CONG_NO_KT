using CongNoGolden.Application.Reminders;
using CongNoGolden.Application.Receipts;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Api.Services;

public sealed class ReminderSchedulerOptions
{
    public bool AutoRunEnabled { get; set; } = true;
    public int PollMinutes { get; set; } = 360;
}

public sealed class ReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderHostedService> _logger;
    private readonly ReminderSchedulerOptions _options;

    public ReminderHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<ReminderSchedulerOptions> options,
        ILogger<ReminderHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoRunEnabled)
        {
            _logger.LogInformation("Reminder scheduler disabled.");
            return;
        }

        var pollMinutes = _options.PollMinutes < 30 ? 30 : _options.PollMinutes;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(pollMinutes));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IReminderService>();
                await service.RunAsync(new ReminderRunRequest(Force: false), stoppingToken);
                var receiptAutomation = scope.ServiceProvider.GetRequiredService<IReceiptAutomationService>();
                await receiptAutomation.RunAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reminder scheduler run failed.");
            }
        }
    }
}
