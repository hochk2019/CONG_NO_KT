using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Infrastructure.Services;

public sealed class ZaloCircuitBreaker
{
    private readonly object _sync = new();
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly ILogger<ZaloCircuitBreaker> _logger;

    private int _consecutiveFailures;
    private DateTimeOffset? _openUntilUtc;

    public ZaloCircuitBreaker(IOptions<ZaloOptions> options, ILogger<ZaloCircuitBreaker> logger)
    {
        var value = options.Value;
        _failureThreshold = Math.Max(1, value.CircuitBreakerFailureThreshold);
        _openDuration = TimeSpan.FromSeconds(Math.Max(1, value.CircuitBreakerOpenSeconds));
        _logger = logger;
    }

    public bool CanExecute(DateTimeOffset nowUtc, out TimeSpan retryAfter)
    {
        lock (_sync)
        {
            if (_openUntilUtc is null)
            {
                retryAfter = TimeSpan.Zero;
                return true;
            }

            if (nowUtc >= _openUntilUtc.Value)
            {
                _openUntilUtc = null;
                _consecutiveFailures = 0;
                retryAfter = TimeSpan.Zero;
                return true;
            }

            retryAfter = _openUntilUtc.Value - nowUtc;
            return false;
        }
    }

    public void RecordSuccess()
    {
        lock (_sync)
        {
            _consecutiveFailures = 0;
            _openUntilUtc = null;
        }
    }

    public void RecordTransientFailure(DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures < _failureThreshold)
            {
                return;
            }

            _consecutiveFailures = 0;
            _openUntilUtc = nowUtc.Add(_openDuration);
            _logger.LogWarning(
                "Zalo circuit opened for {OpenSeconds}s after transient failures.",
                (int)_openDuration.TotalSeconds);
        }
    }
}
