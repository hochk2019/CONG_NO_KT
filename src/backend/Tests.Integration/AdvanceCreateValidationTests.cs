using CongNoGolden.Application.Advances;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class AdvanceCreateValidationTests
{
    private readonly TestDatabaseFixture _fixture;

    public AdvanceCreateValidationTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateAsync_Rejects_EmptyAdvanceNo()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);
        await SeedMasterAsync(db);

        var user = new TestCurrentUser(new[] { "Admin" });
        var service = new AdvanceService(db, user, new AuditService(db, user));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(
                new AdvanceCreateRequest(
                    "SELLER01",
                    "CUST01",
                    "   ",
                    DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    100_000m,
                    null),
                CancellationToken.None));

        Assert.Equal("Advance number is required.", error.Message);
    }

    [Fact]
    public async Task CreateAsync_Rejects_DuplicateAdvanceNo_ForSameSellerAndCustomer()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);
        await SeedMasterAsync(db);

        db.Advances.Add(new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = "SELLER01",
            CustomerTaxCode = "CUST01",
            AdvanceNo = "TH-001",
            AdvanceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Amount = 100_000m,
            OutstandingAmount = 100_000m,
            Status = "DRAFT",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(new[] { "Admin" });
        var service = new AdvanceService(db, user, new AuditService(db, user));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(
                new AdvanceCreateRequest(
                    "SELLER01",
                    "CUST01",
                    "TH-001",
                    DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    200_000m,
                    null),
                CancellationToken.None));

        Assert.Equal(
            "Khoản trả hộ với số chứng từ này đã tồn tại cho người bán và khách hàng này.",
            error.Message);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.receipt_allocations, " +
            "congno.receipts, " +
            "congno.advances, " +
            "congno.customers, " +
            "congno.sellers " +
            "RESTART IDENTITY CASCADE;");
    }

    private static async Task SeedMasterAsync(ConGNoDbContext db)
    {
        db.Sellers.Add(new Seller
        {
            SellerTaxCode = "SELLER01",
            Name = "Seller 01",
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        db.Customers.Add(new Customer
        {
            TaxCode = "CUST01",
            Name = "Customer 01",
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(IReadOnlyList<string> roles)
        {
            Roles = roles;
        }

        public Guid? UserId => Guid.Parse("34343434-3434-3434-3434-343434343434");
        public string? Username => "tester";
        public IReadOnlyList<string> Roles { get; }
        public string? IpAddress => "127.0.0.1";
    }
}
