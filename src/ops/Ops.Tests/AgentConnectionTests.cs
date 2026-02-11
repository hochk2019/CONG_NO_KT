using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ops.Shared.Console;

namespace Ops.Tests;

public class AgentConnectionTests
{
    [Fact]
    public void NormalizeBaseUrl_AddsSchemeAndSlash()
    {
        var url = AgentConnection.NormalizeBaseUrl("127.0.0.1:6090");
        Assert.Equal("http://127.0.0.1:6090/", url);
    }

    [Fact]
    public async Task AgentClient_CanReconfigureAfterRequest()
    {
        var client = new AgentClient(() => new FakeHandler());
        client.Configure("http://localhost:6090", "key");
        await client.GetHealthAsync(CancellationToken.None);

        var ex = Record.Exception(() => client.Configure("http://localhost:6090", "key2"));
        Assert.Null(ex);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"ok\"}")
            };
            return Task.FromResult(response);
        }
    }
}
