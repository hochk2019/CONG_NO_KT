using CongNoGolden.Api.Endpoints;
using CongNoGolden.Application.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class SearchEndpointsTests
{
    [Fact]
    public void MapSearchEndpoints_RegistersGlobalSearchRoute_WithCustomerViewPolicy()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IGlobalSearchService, StubGlobalSearchService>();
        var app = builder.Build();

        app.MapSearchEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/search/global");

        var authorizeMetadata = endpoint.Metadata
            .GetOrderedMetadata<IAuthorizeData>()
            .ToList();

        Assert.Equal("SearchGlobal", endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName);
        Assert.Contains(authorizeMetadata, item => item.Policy == "CustomerView");
    }

    private sealed class StubGlobalSearchService : IGlobalSearchService
    {
        public Task<GlobalSearchResultDto> SearchAsync(string query, int top, CancellationToken ct)
            => Task.FromResult(new GlobalSearchResultDto(query, 0, [], [], []));
    }
}
