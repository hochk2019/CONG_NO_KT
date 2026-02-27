using CongNoGolden.Api.Endpoints;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Maintenance;
using CongNoGolden.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class AdminMaintenanceEndpointsTests
{
    [Fact]
    public void MapAdminMaintenanceEndpoints_RegistersExpectedRoutes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IMaintenanceJobQueue, MaintenanceJobQueue>();
        builder.Services.AddSingleton<ICurrentUser, StubCurrentUser>();
        var app = builder.Build();

        app.MapAdminMaintenanceEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        Assert.Contains(endpoints, e => e.RoutePattern.RawText == "/admin/health/reconcile-balances/queue");
        Assert.Contains(endpoints, e => e.RoutePattern.RawText == "/admin/health/run-retention/queue");
        Assert.Contains(endpoints, e => e.RoutePattern.RawText == "/admin/maintenance/jobs");
        Assert.Contains(endpoints, e => e.RoutePattern.RawText == "/admin/maintenance/jobs/{jobId:guid}");
    }

    private sealed class StubCurrentUser : ICurrentUser
    {
        public Guid? UserId => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string? Username => "test-admin";
        public IReadOnlyList<string> Roles => ["Admin"];
        public string? IpAddress => "127.0.0.1";
    }
}
