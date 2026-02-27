using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class ReceiptLifecycleRbacTests
{
    private readonly TestDatabaseFixture _fixture;

    public ReceiptLifecycleRbacTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateApproveVoidFlow_UpdatesBalancesAndAllocations()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var ownerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        await SeedUserAsync(db, ownerId, "owner-accountant");
        var (seller, customer) = await SeedMasterAsync(db, ownerId);
        var invoice = await SeedInvoiceAsync(db, seller.SellerTaxCode, customer.TaxCode, "INV-1001", 1_000_000m);

        var service = BuildService(db, ownerId, ["Accountant"]);
        var createResult = await service.CreateAsync(
            new ReceiptCreateRequest(
                seller.SellerTaxCode,
                customer.TaxCode,
                "PT-1001",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                1_000_000m,
                "MANUAL",
                null,
                "BANK",
                "Receipt lifecycle test",
                "ISSUE_DATE",
                [new ReceiptTargetRef(invoice.Id, "INVOICE")]),
            CancellationToken.None);

        Assert.Equal("DRAFT", createResult.Status);
        Assert.Equal("SELECTED", createResult.AllocationStatus);

        var approveResult = await service.ApproveAsync(
            createResult.Id,
            new ReceiptApproveRequest(null, createResult.Version),
            CancellationToken.None);

        Assert.Equal(0, approveResult.UnallocatedAmount);
        Assert.Single(approveResult.Lines);
        Assert.Equal("INVOICE", approveResult.Lines[0].TargetType);
        Assert.Equal(1_000_000m, approveResult.Lines[0].Amount);

        var approvedReceipt = await service.GetAsync(createResult.Id, CancellationToken.None);
        Assert.Equal("APPROVED", approvedReceipt.Status);
        Assert.Equal("ALLOCATED", approvedReceipt.AllocationStatus);
        Assert.Equal(1, approvedReceipt.Version);

        var invoiceAfterApprove = await db.Invoices.FirstAsync(i => i.Id == invoice.Id);
        var customerAfterApprove = await db.Customers.FirstAsync(c => c.TaxCode == customer.TaxCode);

        Assert.Equal(0, invoiceAfterApprove.OutstandingAmount);
        Assert.Equal("PAID", invoiceAfterApprove.Status);
        Assert.Equal(-1_000_000m, customerAfterApprove.CurrentBalance);

        var voidResult = await service.VoidAsync(
            createResult.Id,
            new ReceiptVoidRequest("Sai chứng từ", approvedReceipt.Version),
            CancellationToken.None);

        Assert.Equal(1_000_000m, voidResult.ReversedAmount);
        Assert.Equal(1, voidResult.ReversedAllocations);

        var voidedReceipt = await db.Receipts.FirstAsync(r => r.Id == createResult.Id);
        var invoiceAfterVoid = await db.Invoices.FirstAsync(i => i.Id == invoice.Id);
        var customerAfterVoid = await db.Customers.FirstAsync(c => c.TaxCode == customer.TaxCode);

        Assert.Equal("VOID", voidedReceipt.Status);
        Assert.NotNull(voidedReceipt.DeletedAt);
        Assert.Equal(2, voidedReceipt.Version);

        Assert.Equal(1_000_000m, invoiceAfterVoid.OutstandingAmount);
        Assert.Equal("OPEN", invoiceAfterVoid.Status);
        Assert.Equal(0, customerAfterVoid.CurrentBalance);
    }

    [Fact]
    public async Task Approve_WhenAuditFails_RollsBackReceiptAndAllocations()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var ownerId = Guid.Parse("61111111-1111-1111-1111-111111111111");
        await SeedUserAsync(db, ownerId, "owner-audit-fail");
        var (seller, customer) = await SeedMasterAsync(db, ownerId);
        var invoice = await SeedInvoiceAsync(db, seller.SellerTaxCode, customer.TaxCode, "INV-AUD-1", 500_000m);

        var createService = BuildService(db, ownerId, ["Accountant"]);
        var draft = await createService.CreateAsync(
            new ReceiptCreateRequest(
                seller.SellerTaxCode,
                customer.TaxCode,
                "PT-AUD-1",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                500_000m,
                "MANUAL",
                null,
                "BANK",
                "Audit rollback test",
                "ISSUE_DATE",
                [new ReceiptTargetRef(invoice.Id, "INVOICE")]),
            CancellationToken.None);

        var approveService = BuildService(
            db,
            ownerId,
            ["Accountant"],
            new ThrowOnActionAuditService("RECEIPT_APPROVE"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approveService.ApproveAsync(
                draft.Id,
                new ReceiptApproveRequest(null, draft.Version),
                CancellationToken.None));

        var receiptAfterFailure = await db.Receipts
            .AsNoTracking()
            .FirstAsync(r => r.Id == draft.Id);
        var invoiceAfterFailure = await db.Invoices
            .AsNoTracking()
            .FirstAsync(i => i.Id == invoice.Id);
        var customerAfterFailure = await db.Customers
            .AsNoTracking()
            .FirstAsync(c => c.TaxCode == customer.TaxCode);

        Assert.Equal("DRAFT", receiptAfterFailure.Status);
        Assert.Equal("SELECTED", receiptAfterFailure.AllocationStatus);
        Assert.Equal(0m, receiptAfterFailure.UnallocatedAmount);
        Assert.Equal(draft.Version, receiptAfterFailure.Version);

        Assert.Equal(500_000m, invoiceAfterFailure.OutstandingAmount);
        Assert.Equal("OPEN", invoiceAfterFailure.Status);
        Assert.Equal(0m, customerAfterFailure.CurrentBalance);

        var allocationsCount = await db.ReceiptAllocations
            .AsNoTracking()
            .CountAsync(a => a.ReceiptId == draft.Id);
        Assert.Equal(0, allocationsCount);
    }

    [Fact]
    public async Task ListOpenItemsAsync_ReturnsInvoicesAndAdvances_ForOwner()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var ownerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        await SeedUserAsync(db, ownerId, "owner-open-items");
        var (seller, customer) = await SeedMasterAsync(db, ownerId, paymentTermsDays: 15);
        var invoice = await SeedInvoiceAsync(db, seller.SellerTaxCode, customer.TaxCode, "INV-2001", 2_000_000m);
        var advance = await SeedAdvanceAsync(db, seller.SellerTaxCode, customer.TaxCode, "ADV-2001", 500_000m);

        var service = BuildService(db, ownerId, ["Accountant"]);
        var openItems = await service.ListOpenItemsAsync(
            seller.SellerTaxCode,
            customer.TaxCode,
            CancellationToken.None);

        Assert.Equal(2, openItems.Count);

        var invoiceItem = openItems.Single(i => i.TargetType == "INVOICE");
        var advanceItem = openItems.Single(i => i.TargetType == "ADVANCE");

        Assert.Equal(invoice.Id, invoiceItem.TargetId);
        Assert.Equal(2_000_000m, invoiceItem.OutstandingAmount);
        Assert.Equal(invoice.IssueDate.AddDays(15), invoiceItem.DueDate);

        Assert.Equal(advance.Id, advanceItem.TargetId);
        Assert.Equal(500_000m, advanceItem.OutstandingAmount);
        Assert.Equal(advance.AdvanceDate.AddDays(15), advanceItem.DueDate);
    }

    [Fact]
    public async Task Accountant_NotOwner_IsForbidden_ForCreateOpenItemsApproveAndVoid()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var ownerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var outsiderId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var adminId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        await SeedUserAsync(db, ownerId, "owner-rbac");
        await SeedUserAsync(db, outsiderId, "outsider-rbac");
        await SeedUserAsync(db, adminId, "admin-rbac");

        var (seller, customer) = await SeedMasterAsync(db, ownerId);
        var invoice = await SeedInvoiceAsync(db, seller.SellerTaxCode, customer.TaxCode, "INV-3001", 1_200_000m);

        var adminService = BuildService(db, adminId, ["Admin"]);
        var draft = await adminService.CreateAsync(
            new ReceiptCreateRequest(
                seller.SellerTaxCode,
                customer.TaxCode,
                "PT-3001",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                1_200_000m,
                "MANUAL",
                null,
                "BANK",
                "RBAC seed draft",
                "ISSUE_DATE",
                [new ReceiptTargetRef(invoice.Id, "INVOICE")]),
            CancellationToken.None);

        var outsiderService = BuildService(db, outsiderId, ["Accountant"]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            outsiderService.ListOpenItemsAsync(seller.SellerTaxCode, customer.TaxCode, CancellationToken.None));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            outsiderService.CreateAsync(
                new ReceiptCreateRequest(
                    seller.SellerTaxCode,
                    customer.TaxCode,
                    "PT-3002",
                    DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    100_000m,
                    "MANUAL",
                    null,
                    "BANK",
                    "RBAC create",
                    "ISSUE_DATE",
                    [new ReceiptTargetRef(invoice.Id, "INVOICE")]),
                CancellationToken.None));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            outsiderService.ApproveAsync(
                draft.Id,
                new ReceiptApproveRequest(null, draft.Version),
                CancellationToken.None));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            outsiderService.VoidAsync(
                draft.Id,
                new ReceiptVoidRequest("RBAC void", draft.Version),
                CancellationToken.None));
    }

    private static ReceiptService BuildService(
        ConGNoDbContext db,
        Guid userId,
        IReadOnlyList<string> roles,
        IAuditService? auditService = null)
    {
        var currentUser = new TestCurrentUser(userId, roles);
        var resolvedAuditService = auditService ?? new AuditService(db, currentUser);
        return new ReceiptService(db, currentUser, resolvedAuditService);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
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
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private static async Task<(Seller seller, Customer customer)> SeedMasterAsync(
        ConGNoDbContext db,
        Guid ownerId,
        int paymentTermsDays = 0)
    {
        var seller = new Seller
        {
            SellerTaxCode = "SELLER-RBAC",
            Name = "Seller RBAC",
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        var customer = new Customer
        {
            TaxCode = "CUST-RBAC",
            Name = "Customer RBAC",
            AccountantOwnerId = ownerId,
            PaymentTermsDays = paymentTermsDays,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Sellers.Add(seller);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return (seller, customer);
    }

    private static async Task<Invoice> SeedInvoiceAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        string invoiceNo,
        decimal amount)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            InvoiceNo = invoiceNo,
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            RevenueExclVat = amount,
            VatAmount = 0,
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

    private static async Task<Advance> SeedAdvanceAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        string advanceNo,
        decimal amount)
    {
        var advance = new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            AdvanceNo = advanceNo,
            AdvanceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Amount = amount,
            OutstandingAmount = amount,
            Status = "APPROVED",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Advances.Add(advance);
        await db.SaveChangesAsync();
        return advance;
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

    private sealed class ThrowOnActionAuditService : IAuditService
    {
        private readonly string _blockedAction;

        public ThrowOnActionAuditService(string blockedAction)
        {
            _blockedAction = blockedAction;
        }

        public Task LogAsync(string action, string entityType, string entityId, object? before, object? after, CancellationToken ct)
        {
            if (string.Equals(action, _blockedAction, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Audit failure for action {_blockedAction}");
            }

            return Task.CompletedTask;
        }
    }
}
