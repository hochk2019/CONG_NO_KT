using System.Text.Json;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class ReceiptCorrectionTests
{
    private readonly TestDatabaseFixture _fixture;

    public ReceiptCorrectionTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CorrectAsync_UpdatesApprovedMetadataOnly_WithoutReopeningReceipt()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        await SeedMasterAsync(db, userId);

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = "SELLER01",
            CustomerTaxCode = "CUST01",
            ReceiptNo = "RCPT-META-001",
            ReceiptDate = new DateOnly(2026, 3, 1),
            Amount = 200m,
            Method = "BANK",
            Description = "before",
            AllocationMode = "MANUAL",
            AllocationStatus = "UNALLOCATED",
            AllocationPriority = "ISSUE_DATE",
            Status = "APPROVED",
            UnallocatedAmount = 200m,
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Receipts.Add(receipt);

        var customer = await db.Customers.FirstAsync(c => c.TaxCode == "CUST01");
        customer.CurrentBalance = -200m;
        await db.SaveChangesAsync();
        var customerSnapshotBeforeCorrection = await db.Customers
            .AsNoTracking()
            .FirstAsync(c => c.TaxCode == "CUST01");
        var customerVersionBeforeCorrection = customerSnapshotBeforeCorrection.Version;
        var customerUpdatedAtBeforeCorrection = customerSnapshotBeforeCorrection.UpdatedAt;

        var user = new TestCurrentUser(userId, new[] { "Admin" });
        var service = new ReceiptService(db, user, new AuditService(db, user));

        var corrected = await service.CorrectAsync(
            receipt.Id,
            new ReceiptCorrectionRequest(
                ReceiptNo: "RCPT-META-EDIT",
                ReceiptDate: new DateOnly(2026, 3, 2),
                Amount: receipt.Amount,
                AllocationMode: receipt.AllocationMode,
                AppliedPeriodStart: receipt.AppliedPeriodStart,
                Method: "CASH",
                Description: "after",
                AllocationPriority: receipt.AllocationPriority,
                SelectedTargets: null,
                Reason: "metadata correction",
                Version: receipt.Version),
            CancellationToken.None);

        Assert.Equal("APPROVED", corrected.Status);
        Assert.Equal("CASH", corrected.Method);

        var persisted = await db.Receipts.AsNoTracking().FirstAsync(r => r.Id == receipt.Id);
        var refreshedCustomer = await db.Customers.AsNoTracking().FirstAsync(c => c.TaxCode == "CUST01");

        Assert.Equal("RCPT-META-EDIT", persisted.ReceiptNo);
        Assert.Equal(new DateOnly(2026, 3, 2), persisted.ReceiptDate);
        Assert.Equal("CASH", persisted.Method);
        Assert.Equal("after", persisted.Description);
        Assert.Equal("APPROVED", persisted.Status);
        Assert.Equal(-200m, refreshedCustomer.CurrentBalance);
        Assert.Equal(customerVersionBeforeCorrection, refreshedCustomer.Version);
        Assert.Equal(customerUpdatedAtBeforeCorrection, refreshedCustomer.UpdatedAt);

        var audit = await db.AuditLogs.AsNoTracking()
            .SingleAsync(log => log.EntityType == "Receipt" && log.EntityId == receipt.Id.ToString());

        Assert.Equal("RECEIPT_CORRECT", audit.Action);
        Assert.Equal("metadata correction", ReadString(audit.AfterData, "Reason"));
    }

    [Fact]
    public async Task CorrectAsync_ReopensApprovedReceipt_WhenAllocationFieldsChange_AndReversesEffects()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        await SeedMasterAsync(db, userId);
        var invoice = await SeedInvoiceAsync(db, "SELLER01", "CUST01", 800m);
        var invoiceVersionBeforeCorrection = invoice.Version;
        var invoiceUpdatedAtBeforeCorrection = invoice.UpdatedAt;

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = "SELLER01",
            CustomerTaxCode = "CUST01",
            ReceiptNo = "RCPT-ALLOC-001",
            ReceiptDate = new DateOnly(2026, 3, 5),
            Amount = 200m,
            Method = "BANK",
            Description = "before",
            AllocationMode = "MANUAL",
            AllocationStatus = "ALLOCATED",
            AllocationPriority = "ISSUE_DATE",
            AllocationTargets = $"[{{\"id\":\"{invoice.Id}\",\"type\":\"INVOICE\"}}]",
            Status = "APPROVED",
            UnallocatedAmount = 0m,
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Receipts.Add(receipt);
        db.ReceiptAllocations.Add(new ReceiptAllocation
        {
            Id = Guid.NewGuid(),
            ReceiptId = receipt.Id,
            InvoiceId = invoice.Id,
            TargetType = "INVOICE",
            Amount = 200m,
            CreatedAt = DateTimeOffset.UtcNow
        });

        invoice.OutstandingAmount = 600m;
        var customer = await db.Customers.FirstAsync(c => c.TaxCode == "CUST01");
        customer.CurrentBalance = -200m;
        var customerVersionBeforeCorrection = customer.Version;
        var customerUpdatedAtBeforeCorrection = customer.UpdatedAt;
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(userId, new[] { "Admin" });
        var service = new ReceiptService(db, user, new AuditService(db, user));

        var corrected = await service.CorrectAsync(
            receipt.Id,
            new ReceiptCorrectionRequest(
                ReceiptNo: "RCPT-ALLOC-EDIT",
                ReceiptDate: receipt.ReceiptDate,
                Amount: 250m,
                AllocationMode: "FIFO",
                AppliedPeriodStart: new DateOnly(2026, 2, 1),
                Method: receipt.Method,
                Description: "after",
                AllocationPriority: "DUE_DATE",
                SelectedTargets: new[] { new ReceiptTargetRef(invoice.Id, "INVOICE") },
                Reason: "allocation correction",
                Version: receipt.Version),
            CancellationToken.None);

        Assert.Equal("DRAFT", corrected.Status);
        Assert.Equal(250m, corrected.Amount);

        var persisted = await db.Receipts.AsNoTracking().FirstAsync(r => r.Id == receipt.Id);
        var refreshedInvoice = await db.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoice.Id);
        var refreshedCustomer = await db.Customers.AsNoTracking().FirstAsync(c => c.TaxCode == "CUST01");
        var allocations = await db.ReceiptAllocations.AsNoTracking()
            .Where(a => a.ReceiptId == receipt.Id)
            .ToListAsync();

        Assert.Equal("DRAFT", persisted.Status);
        Assert.Equal(250m, persisted.Amount);
        Assert.Equal("DUE_DATE", persisted.AllocationPriority);
        Assert.Equal("MANUAL", persisted.AllocationMode);
        Assert.Equal("SELECTED", persisted.AllocationStatus);
        Assert.Equal(800m, refreshedInvoice.OutstandingAmount);
        Assert.Equal("OPEN", refreshedInvoice.Status);
        Assert.Equal(invoiceVersionBeforeCorrection + 1, refreshedInvoice.Version);
        Assert.NotEqual(invoiceUpdatedAtBeforeCorrection, refreshedInvoice.UpdatedAt);
        Assert.Equal(0m, refreshedCustomer.CurrentBalance);
        Assert.Equal(customerVersionBeforeCorrection + 1, refreshedCustomer.Version);
        Assert.NotEqual(customerUpdatedAtBeforeCorrection, refreshedCustomer.UpdatedAt);
        Assert.Empty(allocations);
    }

    [Fact]
    public async Task CorrectAsync_ReopensApprovedReceipt_WithAdvanceAllocation_AndRestoresMetadata()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var userId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        await SeedMasterAsync(db, userId);
        var advance = await SeedAdvanceAsync(db, "SELLER01", "CUST01", 300m);
        var advanceVersionBeforeCorrection = advance.Version;
        var advanceUpdatedAtBeforeCorrection = advance.UpdatedAt;

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = "SELLER01",
            CustomerTaxCode = "CUST01",
            ReceiptNo = "RCPT-ADV-001",
            ReceiptDate = new DateOnly(2026, 3, 6),
            Amount = 200m,
            Method = "BANK",
            Description = "before",
            AllocationMode = "MANUAL",
            AllocationStatus = "ALLOCATED",
            AllocationPriority = "ISSUE_DATE",
            AllocationTargets = $"[{{\"id\":\"{advance.Id}\",\"type\":\"ADVANCE\"}}]",
            Status = "APPROVED",
            UnallocatedAmount = 0m,
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Receipts.Add(receipt);
        db.ReceiptAllocations.Add(new ReceiptAllocation
        {
            Id = Guid.NewGuid(),
            ReceiptId = receipt.Id,
            AdvanceId = advance.Id,
            TargetType = "ADVANCE",
            Amount = 200m,
            CreatedAt = DateTimeOffset.UtcNow
        });

        advance.OutstandingAmount = 100m;
        advance.Status = "APPROVED";
        var customer = await db.Customers.FirstAsync(c => c.TaxCode == "CUST01");
        customer.CurrentBalance = -200m;
        var customerVersionBeforeCorrection = customer.Version;
        var customerUpdatedAtBeforeCorrection = customer.UpdatedAt;
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(userId, new[] { "Admin" });
        var service = new ReceiptService(db, user, new AuditService(db, user));

        var corrected = await service.CorrectAsync(
            receipt.Id,
            new ReceiptCorrectionRequest(
                ReceiptNo: "RCPT-ADV-EDIT",
                ReceiptDate: receipt.ReceiptDate,
                Amount: 250m,
                AllocationMode: "FIFO",
                AppliedPeriodStart: new DateOnly(2026, 2, 1),
                Method: receipt.Method,
                Description: "after",
                AllocationPriority: "DUE_DATE",
                SelectedTargets: new[] { new ReceiptTargetRef(advance.Id, "ADVANCE") },
                Reason: "advance allocation correction",
                Version: receipt.Version),
            CancellationToken.None);

        Assert.Equal("DRAFT", corrected.Status);
        Assert.Equal(250m, corrected.Amount);

        var persisted = await db.Receipts.AsNoTracking().FirstAsync(r => r.Id == receipt.Id);
        var refreshedAdvance = await db.Advances.AsNoTracking().FirstAsync(a => a.Id == advance.Id);
        var refreshedCustomer = await db.Customers.AsNoTracking().FirstAsync(c => c.TaxCode == "CUST01");
        var allocations = await db.ReceiptAllocations.AsNoTracking()
            .Where(a => a.ReceiptId == receipt.Id)
            .ToListAsync();

        Assert.Equal("DRAFT", persisted.Status);
        Assert.Equal(250m, persisted.Amount);
        Assert.Equal("DUE_DATE", persisted.AllocationPriority);
        Assert.Equal("MANUAL", persisted.AllocationMode);
        Assert.Equal("SELECTED", persisted.AllocationStatus);
        Assert.Equal(300m, refreshedAdvance.OutstandingAmount);
        Assert.Equal("APPROVED", refreshedAdvance.Status);
        Assert.Equal(advanceVersionBeforeCorrection + 1, refreshedAdvance.Version);
        Assert.NotEqual(advanceUpdatedAtBeforeCorrection, refreshedAdvance.UpdatedAt);
        Assert.Equal(0m, refreshedCustomer.CurrentBalance);
        Assert.Equal(customerVersionBeforeCorrection + 1, refreshedCustomer.Version);
        Assert.NotEqual(customerUpdatedAtBeforeCorrection, refreshedCustomer.UpdatedAt);
        Assert.Empty(allocations);
    }

    [Fact]
    public async Task CorrectAsync_RejectsVoidReceipt()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var userId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        await SeedMasterAsync(db, userId);

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = "SELLER01",
            CustomerTaxCode = "CUST01",
            ReceiptNo = "RCPT-VOID-001",
            ReceiptDate = new DateOnly(2026, 3, 8),
            Amount = 120m,
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationStatus = "UNALLOCATED",
            AllocationPriority = "ISSUE_DATE",
            Status = "VOID",
            UnallocatedAmount = 0m,
            DeletedAt = DateTimeOffset.UtcNow,
            DeletedBy = userId,
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 1
        };

        db.Receipts.Add(receipt);
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(userId, new[] { "Admin" });
        var service = new ReceiptService(db, user, new AuditService(db, user));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CorrectAsync(
                receipt.Id,
                new ReceiptCorrectionRequest(
                    ReceiptNo: "RCPT-VOID-EDIT",
                    ReceiptDate: receipt.ReceiptDate,
                    Amount: receipt.Amount,
                    AllocationMode: receipt.AllocationMode,
                    AppliedPeriodStart: receipt.AppliedPeriodStart,
                    Method: receipt.Method,
                    Description: receipt.Description,
                    AllocationPriority: receipt.AllocationPriority,
                    SelectedTargets: null,
                    Reason: "correction reason",
                    Version: receipt.Version),
                CancellationToken.None));

        Assert.Equal("Void receipts cannot be edited.", error.Message);
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
            CurrentBalance = 0m,
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
            InvoiceType = "NORMAL",
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
        decimal amount)
    {
        var advance = new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            AdvanceNo = $"ADV-{Guid.NewGuid():N}".Substring(0, 12),
            AdvanceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-7)),
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

    private static string? ReadString(string? json, string propertyName)
    {
        using var document = JsonDocument.Parse(json ?? throw new InvalidOperationException("Audit payload missing."));
        return document.RootElement.GetProperty(propertyName).GetString();
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
