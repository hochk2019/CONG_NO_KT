using CongNoGolden.Application.Customers;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Api.Services;

public sealed class CustomerBalanceReconcileOptions
{
    public bool AutoRunEnabled { get; set; } = true;
    public int PollMinutes { get; set; } = 720;
    public int MaxItems { get; set; } = 10;
    public decimal Tolerance { get; set; } = 0.01m;
}

public sealed class CustomerBalanceReconcileHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CustomerBalanceReconcileHostedService> _logger;
    private readonly CustomerBalanceReconcileOptions _options;

    public CustomerBalanceReconcileHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<CustomerBalanceReconcileOptions> options,
        ILogger<CustomerBalanceReconcileHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoRunEnabled)
        {
            _logger.LogInformation("Customer balance reconcile scheduler disabled.");
            return;
        }

        var pollMinutes = _options.PollMinutes < 60 ? 60 : _options.PollMinutes;
        var maxItems = _options.MaxItems <= 0 ? 10 : Math.Min(_options.MaxItems, 100);
        var tolerance = _options.Tolerance < 0 ? 0.01m : _options.Tolerance;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(pollMinutes));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ICustomerBalanceReconcileService>();
                var result = await service.ReconcileAsync(
                    new CustomerBalanceReconcileRequest(
                        ApplyChanges: true,
                        MaxItems: maxItems,
                        Tolerance: tolerance),
                    stoppingToken);

                _logger.LogInformation(
                    "Customer balance reconcile done. Checked={Checked} Drifted={Drifted} Updated={Updated} MaxDrift={MaxDrift}",
                    result.CheckedCustomers,
                    result.DriftedCustomers,
                    result.UpdatedCustomers,
                    result.MaxAbsoluteDrift);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Customer balance reconcile run failed.");
            }
        }
    }
}
