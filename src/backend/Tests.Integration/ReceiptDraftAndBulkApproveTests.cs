using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class ReceiptDraftAndBulkApproveTests
{
    private readonly TestDatabaseFixture _fixture;

    public ReceiptDraftAndBulkApproveTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpdateDraftAsync_UpdatesDraftAndIncrementsVersion()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var userId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        await SeedMasterAsync(db, userId);
        var invoice = await SeedInvoiceAsync(db, "SELLER01", "CUST01", 500m);

        var draft = new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = "SELLER01",
            CustomerTaxCode = "CUST01",
            ReceiptNo = "RCPT-001",
            ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Amount = 100m,
            Method = "BANK",
            AllocationMode = "FIFO",
            AllocationStatus = "UNALLOCATED",
            AllocationPriority = "ISSUE_DATE",
            Status = "DRAFT",
            UnallocatedAmount = 0,
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };
        db.Receipts.Add(draft);
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(userId, new[] { "Admin" });
        var service = new ReceiptService(db, user, new AuditService(db, user));

        var updated = await service.UpdateDraftAsync(
            draft.Id,
            new ReceiptDraftUpdateRequest(
                "RCPT-001-EDIT",
                DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1)),
                150m,
                "FIFO",
                null,
                "CASH",
                "edited",
                "DUE_DATE",
                new[] { new ReceiptTargetRef(invoice.Id, "INVOICE") },
                draft.Version),
            CancellationToken.None);

        Assert.Equal("DRAFT", updated.Status);
        Assert.Equal(1, updated.Version);
        Assert.Equal(150m, updated.Amount);
        Assert.Equal("MANUAL", updated.AllocationMode);
        Assert.Equal("SELECTED", updated.AllocationStatus);
        Assert.NotNull(updated.SelectedTargets);
        Assert.Single(updated.SelectedTargets!);
        Assert.Equal(invoice.Id, updated.SelectedTargets![0].Id);
        Assert.Equal("CASH", updated.Method);

        var persisted = await db.Receipts.AsNoTracking().FirstAsync(r => r.Id == draft.Id);
        Assert.Equal("RCPT-001-EDIT", persisted.ReceiptNo);
        Assert.Equal(150m, persisted.Amount);
        Assert.Equal("DRAFT", persisted.Status);
        Assert.Equal(1, persisted.Version);
        Assert.Equal("MANUAL", persisted.AllocationMode);
        Assert.Equal("SELECTED", persisted.AllocationStatus);
    }

    [Fact]
    public async Task ApproveBulkAsync_ApprovesValidRows_AndReportsFailures()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var userId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        await SeedMasterAsync(db, userId);
        var invoice = await SeedInvoiceAsync(db, "SELLER01", "CUST01", 800m);

        var receiptOk = await SeedDraftReceiptAsync(db, userId, "SELLER01", "CUST01", 200m, "RCPT-BULK-1");
        var receiptConflict = await SeedDraftReceiptAsync(db, userId, "SELLER01", "CUST01", 150m, "RCPT-BULK-2");

        var user = new TestCurrentUser(userId, new[] { "Admin" });
        var service = new ReceiptService(db, user, new AuditService(db, user));

        var result = await service.ApproveBulkAsync(
            new ReceiptBulkApproveRequest(
                new[]
                {
                    new ReceiptBulkApproveItem(
                        receiptOk.Id,
                        receiptOk.Version,
                        SelectedTargets: new[] { new ReceiptTargetRef(invoice.Id, "INVOICE") },
                        OverridePeriodLock: true,
                        OverrideReason: "bulk approve integration test"),
                    new ReceiptBulkApproveItem(receiptConflict.Id, receiptConflict.Version + 10)
                },
                ContinueOnError: true),
            CancellationToken.None);

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.Approved);
        Assert.Equal(1, result.Failed);
        Assert.Equal(2, result.Items.Count);

        var okResult = Assert.Single(result.Items.Where(i => i.ReceiptId == receiptOk.Id));
        Assert.Equal("APPROVED", okResult.Result);
        Assert.NotNull(okResult.Preview);

        var failedResult = Assert.Single(result.Items.Where(i => i.ReceiptId == receiptConflict.Id));
        Assert.Equal("FAILED", failedResult.Result);
        Assert.Equal("CONFLICT", failedResult.ErrorCode);
        Assert.NotNull(failedResult.ErrorMessage);

        var persistedOk = await db.Receipts.AsNoTracking().FirstAsync(r => r.Id == receiptOk.Id);
        var persistedConflict = await db.Receipts.AsNoTracking().FirstAsync(r => r.Id == receiptConflict.Id);
        Assert.Equal("APPROVED", persistedOk.Status);
        Assert.Equal("DRAFT", persistedConflict.Status);
    }

    private static async Task ResetAsync(ConGNoDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE " +
            "congno.notifications, " +
            "congno.notification_preferences, " +
            "congno.audit_logs, " +
            "congno.receipt_allocations, " +
            "congno.receipts, " +
            "congno.advances, " +
            "congno.invoices, " +
            "congno.customers, " +
            "congno.sellers, " +
            "congno.user_roles, " +
            "congno.roles, " +
            "congno.users " +
            "RESTART IDENTITY CASCADE;");
    }

    private static async Task SeedMasterAsync(ConGNoDbContext db, Guid userId)
    {
        db.Users.Add(new User
        {
            Id = userId,
            Username = $"u-{userId:N}",
            PasswordHash = "hash",
            FullName = "Test User",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

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
            AccountantOwnerId = userId,
            PaymentTermsDays = 0,
            Status = "ACTIVE",
            CurrentBalance = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private static async Task<Invoice> SeedInvoiceAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        decimal amount)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            InvoiceNo = $"INV-{Guid.NewGuid():N}".Substring(0, 12),
            InvoiceSeries = "AA/26E",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-7)),
            TotalAmount = amount,
            OutstandingAmount = amount,
            Status = "OPEN",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    private static async Task<Receipt> SeedDraftReceiptAsync(
        ConGNoDbContext db,
        Guid userId,
        string sellerTaxCode,
        string customerTaxCode,
        decimal amount,
        string receiptNo)
    {
        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            ReceiptNo = receiptNo,
            ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Amount = amount,
            Method = "BANK",
            AllocationMode = "FIFO",
            AllocationStatus = "UNALLOCATED",
            AllocationPriority = "ISSUE_DATE",
            Status = "DRAFT",
            UnallocatedAmount = 0,
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Receipts.Add(receipt);
        await db.SaveChangesAsync();
        return receipt;
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(Guid userId, IReadOnlyList<string> roles)
        {
            UserId = userId;
            Roles = roles;
        }

        public Guid? UserId { get; }
        public string? Username => "tester";
        public IReadOnlyList<string> Roles { get; }
        public string? IpAddress => "127.0.0.1";
    }
}
