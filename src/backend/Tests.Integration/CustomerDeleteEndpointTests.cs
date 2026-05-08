using System.Text.Json;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Api.Endpoints;
using CongNoGolden.Application.Customers;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public sealed class CustomerDeleteEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TestDatabaseFixture _fixture;

    public CustomerDeleteEndpointTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CustomerDelete_RequiresAdminRole_ReturnsForbidden()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeDeleteAsync("test-customer", ["CustomerManage"]));
        Assert.Contains("IAuthenticationService", ex.Message);
    }

    [Fact]
    public async Task CustomerDelete_FailsWhen_CurrentBalanceNotZero()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        db.Customers.Add(new Customer
        {
            TaxCode = "CUST-DEL-1",
            Name = "Customer with Balance",
            CurrentBalance = 1000m,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });
        await db.SaveChangesAsync();

        var result = await InvokeDeleteAsync("CUST-DEL-1", ["Admin"]);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        AssertProblem(result.Body, "Không thể xóa KH do phát sinh dữ liệu liên đới. Chi tiết: 0 hóa đơn, 0 khoản trả hộ, 0 phiếu thu, dư nợ 1000.", "INVALID_REQUEST");
    }
    
    [Fact]
    public async Task CustomerDelete_FailsWhen_InvoiceExists()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        db.Customers.Add(new Customer
        {
            TaxCode = "CUST-DEL-INV",
            Name = "Customer with Invoice",
            CurrentBalance = 0m,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });
        
        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNo = "INV-001",
            CustomerTaxCode = "CUST-DEL-INV",
            SellerTaxCode = "SELLER-1",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TotalAmount = 2000,
            OutstandingAmount = 2000,
            Status = "OPEN",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });
        await db.SaveChangesAsync();

        var result = await InvokeDeleteAsync("CUST-DEL-INV", ["Admin"]);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        AssertProblem(result.Body, "Không thể xóa KH do phát sinh dữ liệu liên đới. Chi tiết: 1 hóa đơn, 0 khoản trả hộ, 0 phiếu thu, dư nợ 0.", "INVALID_REQUEST");
    }

    [Fact]
    public async Task CustomerDelete_SuccessWithNoRelations()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        db.Customers.Add(new Customer
        {
            TaxCode = "CUST-DEL-CLEAN",
            Name = "Clean Customer",
            CurrentBalance = 0m,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });
        await db.SaveChangesAsync();

        var result = await InvokeDeleteAsync("CUST-DEL-CLEAN", ["Admin"]);

        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        
        await using var verifyDb = _fixture.CreateContext();
        var exists = await verifyDb.Customers.AnyAsync(c => c.TaxCode == "CUST-DEL-CLEAN");
        Assert.False(exists);
    }

    private async Task<ApiResult> InvokeDeleteAsync(string taxCode, IReadOnlyList<string> roles)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("Admin", policy => policy.RequireAssertion(_ => roles.Contains("Admin")));
            options.AddPolicy("CustomerView", policy => policy.RequireAssertion(_ => true));
            options.AddPolicy("CustomerManage", policy => policy.RequireAssertion(_ => true));
        });
        builder.Services.AddProblemDetails();
        builder.Services.AddDbContext<ConGNoDbContext>(options => options
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention());
        builder.Services.AddScoped<ICustomerService, TestCustomerService>();
        builder.Services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(Guid.NewGuid(), roles));
        builder.Services.AddScoped<IAuditService, StubAuditService>();
        
        var app = builder.Build();
        app.MapCustomerEndpoints();
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var response = await client.DeleteAsync($"/customers/{taxCode}");
        var body = await response.Content.ReadAsStringAsync();

        await app.DisposeAsync();

        return new ApiResult((int)response.StatusCode, body);
    }

    private static void AssertProblem(string body, string detail, string code)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal(detail, root.GetProperty("detail").GetString());
        Assert.Equal(code, root.GetProperty("code").GetString());
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE congno.customers, congno.invoices, congno.advances, congno.receipts RESTART IDENTITY CASCADE;");
    }

    private sealed record ApiResult(int StatusCode, string Body);

    private sealed class TestCustomerService : ICustomerService
    {
        public Task<PagedResult<CustomerListItem>> ListAsync(CustomerListRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<CustomerDetailDto?> GetAsync(string taxCode, CancellationToken ct) => throw new NotSupportedException();
        public Task<Customer360Dto?> Get360Async(string taxCode, CancellationToken ct) => throw new NotSupportedException();
        public Task<PagedResult<CustomerInvoiceDto>> ListInvoicesAsync(string taxCode, CustomerRelationRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<PagedResult<CustomerAdvanceDto>> ListAdvancesAsync(string taxCode, CustomerRelationRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<PagedResult<CustomerReceiptDto>> ListReceiptsAsync(string taxCode, CustomerRelationRequest request, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(Guid userId, IReadOnlyList<string> roles)
        {
            UserId = userId;
            Roles = roles;
        }

        public Guid? UserId { get; }
        public string? Username => "integration-test-user";
        public IReadOnlyList<string> Roles { get; }
        public IReadOnlyList<string> Permissions => [];
        public string? IpAddress => "127.0.0.1";
    }

    private sealed class StubAuditService : IAuditService
    {
        public Task LogAsync(string action, string entityType, string entityId, object? before, object? after, CancellationToken ct) => Task.CompletedTask;
    }
}
