using CongNoGolden.Application.Invoices;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Api.Services;

public sealed class InvoiceCreditReconcileOptions
{
    public bool AutoRunEnabled { get; set; } = true;
    public int PollMinutes { get; set; } = 60;
}

public sealed class InvoiceCreditReconcileHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvoiceCreditReconcileHostedService> _logger;
    private readonly InvoiceCreditReconcileOptions _options;

    public InvoiceCreditReconcileHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<InvoiceCreditReconcileOptions> options,
        ILogger<InvoiceCreditReconcileHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoRunEnabled)
        {
            _logger.LogInformation("Invoice credit reconcile scheduler disabled.");
            return;
        }

        var pollMinutes = _options.PollMinutes < 15 ? 15 : _options.PollMinutes;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(pollMinutes));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IInvoiceCreditReconcileService>();
                var result = await service.RunAsync(stoppingToken);
                _logger.LogInformation(
                    "Invoice credit reconcile done. InvoicesUpdated={InvoicesUpdated} ReceiptsUpdated={ReceiptsUpdated} AllocationsCreated={AllocationsCreated}",
                    result.InvoicesUpdated,
                    result.ReceiptsUpdated,
                    result.AllocationsCreated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invoice credit reconcile run failed.");
            }
        }
    }
}
