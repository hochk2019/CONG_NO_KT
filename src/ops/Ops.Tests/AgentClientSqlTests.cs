using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ops.Shared.Console;

namespace Ops.Tests;

public class AgentClientSqlTests
{
    [Fact]
    public async Task PreviewSql_SendsToPreviewEndpoint()
    {
        var handler = new CaptureHandler();
        var client = new AgentClient(() => handler);
        client.Configure("http://localhost:6090", "key");

        await client.PreviewSqlAsync("select 1", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("http://localhost:6090/db/sql/preview", handler.RequestUri?.ToString());
        Assert.NotNull(handler.Body);
        Assert.Contains("select 1", handler.Body);
    }

    [Fact]
    public async Task ExecuteSql_SendsToExecuteEndpoint()
    {
        var handler = new CaptureHandler();
        var client = new AgentClient(() => handler);
        client.Configure("http://localhost:6090", "key");

        await client.ExecuteSqlAsync("update customers set name='a'", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("http://localhost:6090/db/sql/execute", handler.RequestUri?.ToString());
        Assert.NotNull(handler.Body);
        Assert.Contains("update customers", handler.Body);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            if (request.Content is not null)
                Body = await request.Content.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"exitCode\":0,\"stdout\":\"\",\"stderr\":\"\",\"rowsAffected\":1}")
            };
            return response;
        }
    }
}
