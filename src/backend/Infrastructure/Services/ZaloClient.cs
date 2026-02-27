using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CongNoGolden.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Infrastructure.Services;

public sealed class ZaloOptions
{
    public bool Enabled { get; set; }
    public string OaId { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://openapi.zalo.me/v2.0/oa/message";
    public string AccessToken { get; set; } = string.Empty;
    public string WebhookToken { get; set; } = string.Empty;
    public int LinkCodeMinutes { get; set; } = 15;
    public int RetryMaxAttempts { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 250;
    public int RetryMaxDelayMs { get; set; } = 2000;
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public int CircuitBreakerOpenSeconds { get; set; } = 30;
}

public sealed class ZaloClient : IZaloClient
{
    private readonly HttpClient _httpClient;
    private readonly ZaloOptions _options;
    private readonly ZaloCircuitBreaker _circuitBreaker;
    private readonly ILogger<ZaloClient> _logger;

    public ZaloClient(
        HttpClient httpClient,
        IOptions<ZaloOptions> options,
        ZaloCircuitBreaker circuitBreaker,
        ILogger<ZaloClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _circuitBreaker = circuitBreaker;
        _logger = logger;
    }

    public async Task<ZaloSendResult> SendAsync(string userId, string message, CancellationToken ct)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            return new ZaloSendResult(false, "NOT_CONFIGURED");
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return new ZaloSendResult(false, "MISSING_USER_ID");
        }

        if (!_circuitBreaker.CanExecute(DateTimeOffset.UtcNow, out var retryAfter))
        {
            _logger.LogWarning(
                "Zalo circuit is open. Skipping request for {RetryAfterMs}ms.",
                (int)retryAfter.TotalMilliseconds);
            return new ZaloSendResult(false, "CIRCUIT_OPEN");
        }

        var maxAttempts = _options.RetryMaxAttempts <= 0 ? 1 : Math.Min(_options.RetryMaxAttempts, 5);
        var baseDelayMs = _options.RetryBaseDelayMs <= 0 ? 100 : Math.Min(_options.RetryBaseDelayMs, 5000);
        var maxDelayMs = _options.RetryMaxDelayMs < baseDelayMs ? baseDelayMs : Math.Min(_options.RetryMaxDelayMs, 10000);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var payload = new
                {
                    recipient = new { user_id = userId },
                    message = new { text = message }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiBaseUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    _circuitBreaker.RecordSuccess();
                    return new ZaloSendResult(true, null);
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                var code = $"HTTP_{(int)response.StatusCode}";
                if (!IsTransientStatus(response.StatusCode) || attempt >= maxAttempts)
                {
                    if (IsTransientStatus(response.StatusCode))
                    {
                        _circuitBreaker.RecordTransientFailure(DateTimeOffset.UtcNow);
                    }
                    else
                    {
                        _circuitBreaker.RecordSuccess();
                    }

                    _logger.LogWarning("Zalo send failed: {Status} {Body}", response.StatusCode, body);
                    return new ZaloSendResult(false, code);
                }

                var delay = ComputeDelay(attempt, baseDelayMs, maxDelayMs);
                _logger.LogWarning(
                    "Zalo send transient failure (attempt {Attempt}/{MaxAttempts}) status={Status}. Retrying in {DelayMs}ms.",
                    attempt,
                    maxAttempts,
                    response.StatusCode,
                    (int)delay.TotalMilliseconds);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex) when (IsTransientException(ex))
            {
                if (attempt >= maxAttempts)
                {
                    _circuitBreaker.RecordTransientFailure(DateTimeOffset.UtcNow);
                    _logger.LogError(ex, "Zalo send failed after retries.");
                    return new ZaloSendResult(false, "EXCEPTION");
                }

                var delay = ComputeDelay(attempt, baseDelayMs, maxDelayMs);
                _logger.LogWarning(
                    ex,
                    "Zalo send transient exception (attempt {Attempt}/{MaxAttempts}). Retrying in {DelayMs}ms.",
                    attempt,
                    maxAttempts,
                    (int)delay.TotalMilliseconds);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                _circuitBreaker.RecordSuccess();
                _logger.LogError(ex, "Zalo send error.");
                return new ZaloSendResult(false, "EXCEPTION");
            }
        }

        return new ZaloSendResult(false, "EXCEPTION");
    }

    private static bool IsTransientStatus(System.Net.HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code == 408 || code == 429 || code >= 500;
    }

    private static bool IsTransientException(Exception ex)
    {
        return ex is HttpRequestException || ex is TaskCanceledException;
    }

    private static TimeSpan ComputeDelay(int attempt, int baseDelayMs, int maxDelayMs)
    {
        var exponent = Math.Clamp(attempt - 1, 0, 10);
        var delayMs = (int)Math.Min(maxDelayMs, baseDelayMs * Math.Pow(2, exponent));
        return TimeSpan.FromMilliseconds(delayMs);
    }
}
