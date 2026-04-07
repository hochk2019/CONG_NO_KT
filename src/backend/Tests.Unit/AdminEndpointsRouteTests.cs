using CongNoGolden.Api.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class AdminEndpointsRouteTests
{
    [Fact]
    public void MapAdminEndpoints_RegistersPermissionManagementRoutes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        var app = builder.Build();

        app.MapAdminEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        Assert.Contains(endpoints, e => e.RoutePattern.RawText == "/admin/users");
        Assert.Contains(endpoints, e => e.RoutePattern.RawText == "/admin/roles");
        Assert.Contains(endpoints, e => e.RoutePattern.RawText == "/admin/permissions");
        Assert.Contains(endpoints, e => e.RoutePattern.RawText == "/admin/users/{id:guid}/roles");
        Assert.Contains(endpoints, e => e.RoutePattern.RawText == "/admin/roles/{roleId:int}/permissions");
    }
}
