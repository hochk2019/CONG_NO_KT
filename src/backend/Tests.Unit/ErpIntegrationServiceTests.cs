using System.Net;
using System.Text;
using CongNoGolden.Application.Integrations;
using CongNoGolden.Application.Reports;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Tests.Unit;

public sealed class ErpIntegrationServiceTests
{
    [Fact]
    public async Task GetStatusAsync_ReturnsConfigurationFlags()
    {
        var service = CreateService(
            options: new ErpIntegrationOptions
            {
                Enabled = false,
                Provider = "MISA",
                BaseUrl = "",
                ApiKey = "",
                CompanyCode = "",
                TimeoutSeconds = 20
            },
            httpHandler: new StubHttpMessageHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))),
            reportService: new StubReportService(DefaultKpis));

        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.Equal("MISA", status.Provider);
        Assert.False(status.Enabled);
        Assert.False(status.Configured);
        Assert.False(status.HasApiKey);
        Assert.Equal(20, status.TimeoutSeconds);
    }

    [Fact]
    public async Task SyncSummaryAsync_DryRun_DoesNotCallRemote()
    {
        var callCount = 0;
        var service = CreateService(
            options: new ErpIntegrationOptions
            {
                Enabled = true,
                Provider = "MISA",
                BaseUrl = "https://erp.example.com",
                ApiKey = "demo-key",
                CompanyCode = "GL01",
                TimeoutSeconds = 15
            },
            httpHandler: new StubHttpMessageHandler((_, _) =>
            {
                callCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }),
            reportService: new StubReportService(DefaultKpis));

        var result = await service.SyncSummaryAsync(
            new ErpSyncSummaryRequest(
                From: new DateOnly(2026, 1, 1),
                To: new DateOnly(2026, 1, 31),
                AsOfDate: new DateOnly(2026, 1, 31),
                DueSoonDays: 7,
                DryRun: true,
                RequestedBy: "tester"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("dry_run", result.Status);
        Assert.Equal(0, callCount);
        Assert.Equal(5_000m, result.Payload.TotalOutstanding);
        Assert.Equal(2_500m, result.Payload.OverdueAmount);
    }

    [Fact]
    public async Task SyncSummaryAsync_EnabledAndConfigured_PostsSummaryPayload()
    {
        var callCount = 0;
        string? body = null;
        string? requestUri = null;
        string? apiKey = null;

        var service = CreateService(
            options: new ErpIntegrationOptions
            {
                Enabled = true,
                Provider = "MISA",
                BaseUrl = "https://erp.example.com",
                ApiKey = "demo-key",
                CompanyCode = "GL01",
                TimeoutSeconds = 15
            },
            httpHandler: new StubHttpMessageHandler(async (request, ct) =>
            {
                callCount++;
                requestUri = request.RequestUri?.ToString();
                apiKey = request.Headers.TryGetValues("X-Api-Key", out var values)
                    ? values.SingleOrDefault()
                    : null;
                body = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(ct);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"status\":\"ok\"}", Encoding.UTF8, "application/json")
                };
            }),
            reportService: new StubReportService(DefaultKpis));

        var result = await service.SyncSummaryAsync(
            new ErpSyncSummaryRequest(
                From: new DateOnly(2026, 1, 1),
                To: new DateOnly(2026, 1, 31),
                AsOfDate: new DateOnly(2026, 1, 31),
                DueSoonDays: 7,
                DryRun: false,
                RequestedBy: "tester"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("success", result.Status);
        Assert.Equal(1, callCount);
        Assert.Equal("https://erp.example.com/sync/summary", requestUri);
        Assert.Equal("demo-key", apiKey);
        Assert.NotNull(body);
        Assert.Contains("\"sourceSystem\":\"congno-golden\"", body);
        Assert.Contains("\"totalOutstanding\":5000", body);
    }

    [Fact]
    public async Task UpdateConfigAsync_PersistsConfig_AndKeepsApiKeyWhenBlank()
    {
        await using var db = CreateDbContext();
        var service = CreateService(
            options: new ErpIntegrationOptions
            {
                Enabled = false,
                Provider = "MISA",
                BaseUrl = "",
                ApiKey = "",
                CompanyCode = "",
                TimeoutSeconds = 15
            },
            httpHandler: new StubHttpMessageHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))),
            reportService: new StubReportService(DefaultKpis),
            db: db);

        var first = await service.UpdateConfigAsync(
            new ErpIntegrationConfigUpdateRequest(
                Enabled: false,
                Provider: "MISA",
                BaseUrl: "https://erp.example.com",
                CompanyCode: "GL01",
                TimeoutSeconds: 30,
                ApiKey: "first-key",
                ClearApiKey: false),
            updatedBy: "admin",
            CancellationToken.None);

        Assert.True(first.HasApiKey);
        Assert.Equal("https://erp.example.com", first.BaseUrl);
        Assert.Equal(30, first.TimeoutSeconds);

        var second = await service.UpdateConfigAsync(
            new ErpIntegrationConfigUpdateRequest(
                Enabled: true,
                Provider: "MISA",
                BaseUrl: "https://erp.example.com",
                CompanyCode: "GL01",
                TimeoutSeconds: 25,
                ApiKey: null,
                ClearApiKey: false),
            updatedBy: "admin",
            CancellationToken.None);

        Assert.True(second.Enabled);
        Assert.True(second.HasApiKey);
        Assert.Equal(25, second.TimeoutSeconds);

        var status = await service.GetStatusAsync(CancellationToken.None);
        Assert.True(status.Enabled);
        Assert.True(status.Configured);
    }

    private static ReportKpiDto DefaultKpis => new(
        TotalOutstanding: 5_000m,
        OutstandingInvoice: 4_000m,
        OutstandingAdvance: 1_000m,
        UnallocatedReceiptsAmount: 100m,
        UnallocatedReceiptsCount: 2,
        OverdueAmount: 2_500m,
        OverdueCustomers: 3,
        DueSoonAmount: 700m,
        DueSoonCustomers: 2,
        OnTimeCustomers: 5);

    private static ErpIntegrationService CreateService(
        ErpIntegrationOptions options,
        HttpMessageHandler httpHandler,
        IReportService reportService,
        ConGNoDbContext? db = null)
    {
        db ??= CreateDbContext();
        return new ErpIntegrationService(
            new HttpClient(httpHandler),
            db,
            Options.Create(options),
            reportService,
            NullLogger<ErpIntegrationService>.Instance);
    }

    private static ConGNoDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"erp-integration-service-{Guid.NewGuid():N}")
            .Options;
        return new ConGNoDbContext(options);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _callback;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback)
        {
            _callback = callback;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _callback(request, cancellationToken);
        }
    }

    private sealed class StubReportService : IReportService
    {
        private readonly ReportKpiDto _kpis;

        public StubReportService(ReportKpiDto kpis)
        {
            _kpis = kpis;
        }

        public Task<ReportKpiDto> GetKpisAsync(ReportKpiRequest request, CancellationToken ct)
        {
            return Task.FromResult(_kpis);
        }

        public Task<IReadOnlyList<ReportSummaryRow>> GetSummaryAsync(ReportSummaryRequest request, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<PagedResult<ReportSummaryRow>> GetSummaryPagedAsync(ReportSummaryPagedRequest request, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<ReportStatementResult> GetStatementAsync(ReportStatementRequest request, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<ReportStatementPagedResult> GetStatementPagedAsync(ReportStatementPagedRequest request, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ReportAgingRow>> GetAgingAsync(ReportAgingRequest request, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<PagedResult<ReportAgingRow>> GetAgingPagedAsync(ReportAgingPagedRequest request, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<ReportChartsDto> GetChartsAsync(ReportChartsRequest request, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<ReportInsightsDto> GetInsightsAsync(ReportInsightsRequest request, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<ReportPreferencesDto> GetPreferencesAsync(Guid userId, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<ReportPreferencesDto> UpdatePreferencesAsync(Guid userId, UpdateReportPreferencesRequest request, CancellationToken ct)
            => throw new NotImplementedException();
    }
}
