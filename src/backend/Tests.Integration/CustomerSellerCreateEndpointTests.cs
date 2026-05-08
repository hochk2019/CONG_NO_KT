using System.Text;
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
public sealed class CustomerSellerCreateEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TestDatabaseFixture _fixture;

    public CustomerSellerCreateEndpointTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CustomerCreate_CreatesRecordAndReturnsCreated()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var ownerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var managerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        await SeedUserAsync(db, ownerId, "owner-accountant", "Owner Accountant");
        await SeedUserAsync(db, managerId, "manager-user", "Manager User");

        var now = DateTimeOffset.UtcNow;
        var result = await InvokePostAsync(
            app => app.MapCustomerEndpoints(),
            "/customers",
            new CustomerCreateRequest(
                " cust-new-01 ",
                "  Cong ty Moi  ",
                "  123 Nguyen Hue  ",
                " finance@example.com ",
                " 0909000000 ",
                "active",
                45,
                500_000_000m,
                ownerId,
                managerId));

        Assert.True(
            result.StatusCode == StatusCodes.Status201Created,
            $"Expected 201 but got {result.StatusCode}. Body: {result.Body}");
        Assert.Equal("/customers/CUST-NEW-01", result.Location);

        var payload = AssertDeserialize<CustomerDetailDto>(result.Body);
        Assert.Equal("CUST-NEW-01", payload.TaxCode);
        Assert.Equal("Cong ty Moi", payload.Name);
        Assert.Equal("123 Nguyen Hue", payload.Address);
        Assert.Equal("finance@example.com", payload.Email);
        Assert.Equal("0909000000", payload.Phone);
        Assert.Equal("ACTIVE", payload.Status);
        Assert.Equal(45, payload.PaymentTermsDays);
        Assert.Equal(500_000_000m, payload.CreditLimit);
        Assert.Equal(0m, payload.CurrentBalance);
        Assert.Equal(ownerId, payload.OwnerId);
        Assert.Equal("Owner Accountant", payload.OwnerName);
        Assert.Equal(managerId, payload.ManagerId);
        Assert.Equal("Manager User", payload.ManagerName);
        Assert.True(payload.CreatedAt >= now.AddMinutes(-1));

        await using var verifyDb = _fixture.CreateContext();
        var customer = await verifyDb.Customers
            .AsNoTracking()
            .SingleAsync(c => c.TaxCode == "CUST-NEW-01");

        Assert.Equal("Cong ty Moi", customer.Name);
        Assert.Equal("123 Nguyen Hue", customer.Address);
        Assert.Equal("finance@example.com", customer.Email);
        Assert.Equal("0909000000", customer.Phone);
        Assert.Equal("ACTIVE", customer.Status);
        Assert.Equal(45, customer.PaymentTermsDays);
        Assert.Equal(500_000_000m, customer.CreditLimit);
        Assert.Equal(0m, customer.CurrentBalance);
        Assert.Equal(ownerId, customer.AccountantOwnerId);
        Assert.Equal(managerId, customer.ManagerUserId);
    }

    [Fact]
    public async Task CustomerCreate_RejectsUnknownOwner()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var missingOwnerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var result = await InvokePostAsync(
            app => app.MapCustomerEndpoints(),
            "/customers",
            new CustomerCreateRequest(
                "cust-unknown-owner",
                "Customer Missing Owner",
                null,
                null,
                null,
                "ACTIVE",
                0,
                null,
                missingOwnerId,
                null));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        AssertProblem(result.Body, "Owner user not found.", "INVALID_REQUEST");
    }

    [Fact]
    public async Task SellerCreate_CreatesRecordAndReturnsCreated()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var result = await InvokePostAsync(
            app => app.MapLookupEndpoints(),
            "/sellers",
            new SellerCreateRequest(
                " seller-001 ",
                "  Seller One  ",
                "  S1  ",
                "  456 Tran Hung Dao  ",
                "inactive"));

        Assert.True(
            result.StatusCode == StatusCodes.Status201Created,
            $"Expected 201 but got {result.StatusCode}. Body: {result.Body}");
        Assert.Equal("/sellers/SELLER-001", result.Location);

        var payload = AssertDeserialize<SellerLookupItem>(result.Body);
        Assert.Equal("SELLER-001", payload.TaxCode);
        Assert.Equal("Seller One", payload.Name);
        Assert.Equal("S1", payload.ShortName);

        await using var verifyDb = _fixture.CreateContext();
        var seller = await verifyDb.Sellers
            .AsNoTracking()
            .SingleAsync(s => s.SellerTaxCode == "SELLER-001");

        Assert.Equal("Seller One", seller.Name);
        Assert.Equal("S1", seller.ShortName);
        Assert.Equal("456 Tran Hung Dao", seller.Address);
        Assert.Equal("INACTIVE", seller.Status);
    }

    [Fact]
    public async Task SellerCreate_RejectsDuplicateTaxCode()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        db.Sellers.Add(new Seller
        {
            SellerTaxCode = "SELLER-001",
            Name = "Existing Seller",
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });
        await db.SaveChangesAsync();

        var result = await InvokePostAsync(
            app => app.MapLookupEndpoints(),
            "/sellers",
            new SellerCreateRequest(
                "seller-001",
                "Another Seller",
                null,
                null,
                "ACTIVE"));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        AssertProblem(result.Body, "Seller tax code already exists.", "INVALID_REQUEST");
    }

    private async Task<ApiResult> InvokePostAsync(
        Action<IEndpointRouteBuilder> mapEndpoints,
        string routePattern,
        object request)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("CustomerManage", policy => policy.RequireAssertion(_ => true));
            options.AddPolicy("CustomerView", policy => policy.RequireAssertion(_ => true));
        });
        builder.Services.AddProblemDetails();
        builder.Services.AddDbContext<ConGNoDbContext>(options => options
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention());
        builder.Services.AddScoped<ICustomerService, TestCustomerService>();
        builder.Services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), ["Admin"]));
        builder.Services.AddScoped<IAuditService, StubAuditService>();

        var app = builder.Build();
        mapEndpoints(app);
        await app.StartAsync();

        var requestJson = JsonSerializer.Serialize(request, JsonOptions);
        using var client = app.GetTestClient();
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(routePattern, content);
        var body = await response.Content.ReadAsStringAsync();

        await app.DisposeAsync();

        return new ApiResult(
            (int)response.StatusCode,
            response.Headers.Location?.ToString(),
            body);
    }

    private static T AssertDeserialize<T>(string body)
    {
        var payload = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return Assert.IsType<T>(payload);
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
            "TRUNCATE TABLE congno.customers, congno.sellers, congno.users RESTART IDENTITY CASCADE;");
    }

    private static async Task SeedUserAsync(ConGNoDbContext db, Guid userId, string username, string fullName)
    {
        db.Users.Add(new User
        {
            Id = userId,
            Username = username,
            PasswordHash = "hash",
            FullName = fullName,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private sealed record ApiResult(int StatusCode, string? Location, string Body);

    private sealed class TestCustomerService : ICustomerService
    {
        public Task<PagedResult<CustomerListItem>> ListAsync(CustomerListRequest request, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<CustomerDetailDto?> GetAsync(string taxCode, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<Customer360Dto?> Get360Async(string taxCode, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PagedResult<CustomerInvoiceDto>> ListInvoicesAsync(string taxCode, CustomerRelationRequest request, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PagedResult<CustomerAdvanceDto>> ListAdvancesAsync(string taxCode, CustomerRelationRequest request, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PagedResult<CustomerReceiptDto>> ListReceiptsAsync(string taxCode, CustomerRelationRequest request, CancellationToken ct) =>
            throw new NotSupportedException();
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
        public Task LogAsync(string action, string entityType, string entityId, object? before, object? after, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
