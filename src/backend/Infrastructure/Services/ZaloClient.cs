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
}

public sealed class ZaloClient : IZaloClient
{
    private readonly HttpClient _httpClient;
    private readonly ZaloOptions _options;
    private readonly ILogger<ZaloClient> _logger;

    public ZaloClient(HttpClient httpClient, IOptions<ZaloOptions> options, ILogger<ZaloClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
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
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Zalo send failed: {Status} {Body}", response.StatusCode, body);
                return new ZaloSendResult(false, $"HTTP_{(int)response.StatusCode}");
            }

            return new ZaloSendResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zalo send error.");
            return new ZaloSendResult(false, "EXCEPTION");
        }
    }
}
