using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class ReceiptSurplusQueueTests
{
    private readonly TestDatabaseFixture _fixture;

    public ReceiptSurplusQueueTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListSurplusQueueAsync_ReturnsReceiptsAndHeldCredits()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var ownerId = Guid.Parse("71111111-1111-1111-1111-111111111111");
        await SeedUserAsync(db, ownerId, "owner-surplus");
        var seller = await SeedSellerAsync(db, "SELLER-SURPLUS", "Seller Surplus");
        var customer = await SeedCustomerAsync(db, "CUST-SURPLUS", "Customer Surplus", ownerId);

        var unallocatedReceipt = await SeedReceiptAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            "PT-UN-001",
            new DateOnly(2026, 3, 6),
            "UNALLOCATED",
            600_000m);

        var partialReceipt = await SeedReceiptAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            "PT-PA-001",
            new DateOnly(2026, 3, 5),
            "PARTIAL",
            200_000m);

        await SeedReceiptAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            "PT-AL-001",
            new DateOnly(2026, 3, 4),
            "ALLOCATED",
            0m);

        var sourceReceipt = await SeedReceiptAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            "PT-HC-001",
            new DateOnly(2026, 3, 3),
            "ALLOCATED",
            0m);

        var originalInvoice = await SeedInvoiceAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            "INV-HC-001",
            new DateOnly(2026, 3, 1),
            300_000m);

        await SeedHeldCreditAsync(
            db,
            sourceReceipt.Id,
            originalInvoice.Id,
            "HOLDING",
            300_000m,
            ownerId);

        await SeedHeldCreditAsync(
            db,
            sourceReceipt.Id,
            originalInvoice.Id,
            "REAPPLIED",
            0m,
            ownerId);

        var service = BuildReceiptService(db, ownerId, ["Admin"]);

        var result = await service.ListSurplusQueueAsync(
            new ReceiptSurplusQueueRequest(
                ItemType: null,
                Search: null,
                SellerTaxCode: null,
                CustomerTaxCode: null,
                From: null,
                To: null,
                AmountMin: null,
                AmountMax: null,
                Page: 1,
                PageSize: 20),
            CancellationToken.None);

        Assert.Equal(3, result.Total);
        Assert.Collection(
            result.Items,
            item =>
            {
                Assert.Equal(unallocatedReceipt.Id, item.Id);
                Assert.Equal("UNALLOCATED_RECEIPT", item.ItemType);
                Assert.Equal("UNALLOCATED", item.Status);
                Assert.Equal(600_000m, item.AmountRemaining);
                Assert.Equal("Customer Surplus", item.CustomerName);
                Assert.Equal("owner-surplus", item.OwnerName);
                Assert.True(item.CanManage);
            },
            item =>
            {
                Assert.Equal(partialReceipt.Id, item.Id);
                Assert.Equal("PARTIAL_RECEIPT", item.ItemType);
                Assert.Equal("PARTIAL", item.Status);
                Assert.Equal(200_000m, item.AmountRemaining);
            },
            item =>
            {
                Assert.Equal("HELD_CREDIT", item.ItemType);
                Assert.Equal("HOLDING", item.Status);
                Assert.Equal(sourceReceipt.Id, item.ReceiptId);
                Assert.Equal("PT-HC-001", item.ReceiptNo);
                Assert.Equal("INV-HC-001", item.OriginalInvoiceNo);
                Assert.Equal(300_000m, item.AmountRemaining);
            });
    }

    [Fact]
    public async Task ListSurplusQueueAsync_FilterHeldCreditsBySearch_ReturnsMatchingRows()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var ownerId = Guid.Parse("72222222-2222-2222-2222-222222222222");
        await SeedUserAsync(db, ownerId, "owner-surplus-search");
        var seller = await SeedSellerAsync(db, "SELLER-SRCH", "Seller Search");
        var customer = await SeedCustomerAsync(db, "CUST-SRCH", "Customer Search", ownerId);

        var sourceReceipt = await SeedReceiptAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            "PT-HC-SEARCH",
            new DateOnly(2026, 3, 2),
            "ALLOCATED",
            0m);

        var matchingInvoice = await SeedInvoiceAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            "INV-MATCH-001",
            new DateOnly(2026, 3, 1),
            400_000m);

        var otherInvoice = await SeedInvoiceAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            "INV-OTHER-002",
            new DateOnly(2026, 2, 28),
            250_000m);

        await SeedHeldCreditAsync(db, sourceReceipt.Id, matchingInvoice.Id, "HOLDING", 400_000m, ownerId);
        await SeedHeldCreditAsync(db, sourceReceipt.Id, otherInvoice.Id, "PARTIAL", 150_000m, ownerId);

        var service = BuildReceiptService(db, ownerId, ["Admin"]);

        var result = await service.ListSurplusQueueAsync(
            new ReceiptSurplusQueueRequest(
                ItemType: "HELD_CREDIT",
                Search: "MATCH-001",
                SellerTaxCode: seller.SellerTaxCode,
                CustomerTaxCode: customer.TaxCode,
                From: null,
                To: null,
                AmountMin: null,
                AmountMax: null,
                Page: 1,
                PageSize: 20),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("HELD_CREDIT", item.ItemType);
        Assert.Equal("INV-MATCH-001", item.OriginalInvoiceNo);
        Assert.Equal(400_000m, item.AmountRemaining);
    }

    [Fact]
    public async Task ListSurplusQueueAsync_NonPrivilegedUser_OnlySeesOwnCustomers()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var ownerA = Guid.Parse("73333333-3333-3333-3333-333333333333");
        var ownerB = Guid.Parse("74444444-4444-4444-4444-444444444444");
        await SeedUserAsync(db, ownerA, "owner-a");
        await SeedUserAsync(db, ownerB, "owner-b");

        var seller = await SeedSellerAsync(db, "SELLER-OWN", "Seller Owner");
        var customerA = await SeedCustomerAsync(db, "CUST-OWN-A", "Customer Owner A", ownerA);
        var customerB = await SeedCustomerAsync(db, "CUST-OWN-B", "Customer Owner B", ownerB);

        await SeedReceiptAsync(
            db,
            seller.SellerTaxCode,
            customerA.TaxCode,
            "PT-OWN-A",
            new DateOnly(2026, 3, 7),
            "UNALLOCATED",
            100_000m);

        await SeedReceiptAsync(
            db,
            seller.SellerTaxCode,
            customerB.TaxCode,
            "PT-OWN-B",
            new DateOnly(2026, 3, 7),
            "UNALLOCATED",
            200_000m);

        var service = BuildReceiptService(db, ownerA, []);

        var result = await service.ListSurplusQueueAsync(
            new ReceiptSurplusQueueRequest(
                ItemType: null,
                Search: null,
                SellerTaxCode: null,
                CustomerTaxCode: null,
                From: null,
                To: null,
                AmountMin: null,
                AmountMax: null,
                Page: 1,
                PageSize: 20),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("PT-OWN-A", item.ReceiptNo);
        Assert.Equal("Customer Owner A", item.CustomerName);
        Assert.Equal("owner-a", item.OwnerName);
        Assert.True(item.CanManage);
    }

    private static ReceiptService BuildReceiptService(
        ConGNoDbContext db,
        Guid userId,
        IReadOnlyList<string> roles)
    {
        var currentUser = new TestCurrentUser(userId, roles);
        return new ReceiptService(db, currentUser, new AuditService(db, currentUser));
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.receipt_held_credits, " +
            "congno.receipt_allocations, " +
            "congno.receipts, " +
            "congno.invoices, " +
            "congno.advances, " +
            "congno.customers, " +
            "congno.sellers, " +
            "congno.users " +
            "RESTART IDENTITY CASCADE;");
    }

    private static async Task SeedUserAsync(ConGNoDbContext db, Guid userId, string username)
    {
        db.Users.Add(new User
        {
            Id = userId,
            Username = username,
            PasswordHash = "hash",
            FullName = null,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private static async Task<Seller> SeedSellerAsync(ConGNoDbContext db, string taxCode, string name)
    {
        var seller = new Seller
        {
            SellerTaxCode = taxCode,
            Name = name,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Sellers.Add(seller);
        await db.SaveChangesAsync();
        return seller;
    }

    private static async Task<Customer> SeedCustomerAsync(
        ConGNoDbContext db,
        string taxCode,
        string name,
        Guid ownerId)
    {
        var customer = new Customer
        {
            TaxCode = taxCode,
            Name = name,
            AccountantOwnerId = ownerId,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    private static async Task<Receipt> SeedReceiptAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        string receiptNo,
        DateOnly receiptDate,
        string allocationStatus,
        decimal unallocatedAmount)
    {
        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            ReceiptNo = receiptNo,
            ReceiptDate = receiptDate,
            Amount = Math.Max(1_000m, unallocatedAmount == 0 ? 1_000_000m : unallocatedAmount),
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationStatus = allocationStatus,
            AllocationPriority = "ISSUE_DATE",
            Status = "APPROVED",
            UnallocatedAmount = unallocatedAmount,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Receipts.Add(receipt);
        await db.SaveChangesAsync();
        return receipt;
    }

    private static async Task<Invoice> SeedInvoiceAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        string invoiceNo,
        DateOnly issueDate,
        decimal amount)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            InvoiceNo = invoiceNo,
            IssueDate = issueDate,
            RevenueExclVat = amount,
            VatAmount = 0m,
            TotalAmount = amount,
            OutstandingAmount = amount,
            InvoiceType = "SALE",
            Status = "OPEN",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    private static async Task SeedHeldCreditAsync(
        ConGNoDbContext db,
        Guid receiptId,
        Guid originalInvoiceId,
        string status,
        decimal amountRemaining,
        Guid createdBy)
    {
        db.ReceiptHeldCredits.Add(new ReceiptHeldCredit
        {
            Id = Guid.NewGuid(),
            ReceiptId = receiptId,
            OriginalInvoiceId = originalInvoiceId,
            OriginalAmount = Math.Max(amountRemaining, 1m),
            AmountRemaining = amountRemaining,
            Status = status,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
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
        public string? IpAddress => "127.0.0.1";
    }
}
