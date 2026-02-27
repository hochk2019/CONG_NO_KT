using System.Net;
using System.Net.Http.Headers;
using CongNoGolden.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Tests.Unit;

public sealed class ZaloClientRetryTests
{
    [Fact]
    public async Task SendAsync_RetriesTransientFailure_AndSucceeds()
    {
        using var handler = new QueueHttpMessageHandler(new object[]
        {
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server_error")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            }
        });
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient, enabled: true);

        var result = await client.SendAsync("user-1", "hello", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task SendAsync_DoesNotRetryNonTransient4xx()
    {
        using var handler = new QueueHttpMessageHandler(new object[]
        {
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("bad_request")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            }
        });
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient, enabled: true);

        var result = await client.SendAsync("user-1", "hello", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("HTTP_400", result.Error);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SendAsync_OpensCircuitAndSkipsCall_DuringCooldown()
    {
        using var handler = new QueueHttpMessageHandler(new object[]
        {
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server_error")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            }
        });
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(
            httpClient,
            enabled: true,
            configure: options =>
            {
                options.RetryMaxAttempts = 1;
                options.CircuitBreakerFailureThreshold = 1;
                options.CircuitBreakerOpenSeconds = 30;
            });

        var first = await client.SendAsync("user-1", "hello", CancellationToken.None);
        var second = await client.SendAsync("user-1", "hello", CancellationToken.None);

        Assert.False(first.Success);
        Assert.Equal("HTTP_500", first.Error);
        Assert.False(second.Success);
        Assert.Equal("CIRCUIT_OPEN", second.Error);
        Assert.Equal(1, handler.CallCount);
    }

    private static ZaloClient CreateClient(
        HttpClient httpClient,
        bool enabled,
        Action<ZaloOptions>? configure = null)
    {
        var zaloOptions = new ZaloOptions
        {
            Enabled = enabled,
            AccessToken = "test-access-token",
            ApiBaseUrl = "https://example.test/zalo",
            RetryMaxAttempts = 3,
            RetryBaseDelayMs = 1,
            RetryMaxDelayMs = 1,
            CircuitBreakerFailureThreshold = 5,
            CircuitBreakerOpenSeconds = 30
        };
        configure?.Invoke(zaloOptions);
        var options = Options.Create(zaloOptions);
        var circuitBreaker = new ZaloCircuitBreaker(options, NullLogger<ZaloCircuitBreaker>.Instance);

        return new ZaloClient(httpClient, options, circuitBreaker, NullLogger<ZaloClient>.Instance);
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<object> _responses;

        public QueueHttpMessageHandler(IEnumerable<object> responses)
        {
            _responses = new Queue<object>(responses);
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.NotNull(request.Content);
            Assert.NotNull(request.Headers.Authorization);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.False(string.IsNullOrWhiteSpace(request.Headers.Authorization.Parameter));

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No queued response.");
            }

            var next = _responses.Dequeue();
            if (next is Exception ex)
            {
                throw ex;
            }

            return Task.FromResult((HttpResponseMessage)next);
        }
    }
}
