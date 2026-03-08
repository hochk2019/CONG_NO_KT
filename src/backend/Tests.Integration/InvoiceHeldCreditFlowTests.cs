using System.Data.Common;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Invoices;
using CongNoGolden.Application.ReceiptHeldCredits;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public class InvoiceHeldCreditFlowTests
{
    private readonly TestDatabaseFixture _fixture;

    public InvoiceHeldCreditFlowTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task VoidInvoice_WithReceiptAllocations_CreatesHeldCreditWithoutReplacementInvoice()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var adminId = Guid.Parse("91111111-1111-1111-1111-111111111111");
        await SeedUserAsync(db, adminId, "admin-held-credit");
        var (seller, customer) = await SeedMasterAsync(db, adminId);
        var invoice = await SeedPostedInvoiceAsync(db, seller.SellerTaxCode, customer.TaxCode, "INV-HOLD-001", 1_000_000m);

        var receiptService = BuildReceiptService(db, adminId, ["Admin"]);
        var approvedReceipt = await CreateApprovedReceiptAsync(
            receiptService,
            seller.SellerTaxCode,
            customer.TaxCode,
            "PT-HOLD-001",
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            1_000_000m,
            [new ReceiptTargetRef(invoice.Id, "INVOICE")]);

        var invoiceService = BuildInvoiceService(db, adminId, ["Admin"]);

        var result = await invoiceService.VoidAsync(
            invoice.Id,
            new InvoiceVoidRequest(
                "Void to create held credit",
                null,
                Force: true,
                Version: invoice.Version),
            CancellationToken.None);

        Assert.Equal("VOID", result.Status);

        var invoiceAllocations = await db.ReceiptAllocations
            .AsNoTracking()
            .Where(a => a.InvoiceId == invoice.Id)
            .CountAsync();
        Assert.Equal(0, invoiceAllocations);

        var heldCredits = await ExecuteScalarAsync<long>(
            db,
            """
            select count(*)
            from congno.receipt_held_credits
            where receipt_id = @receipt_id
            """,
            ("receipt_id", approvedReceipt.Id));
        Assert.Equal(1, heldCredits);

        var heldAmount = await ExecuteScalarAsync<decimal>(
            db,
            """
            select coalesce(sum(amount_remaining), 0)
            from congno.receipt_held_credits
            where receipt_id = @receipt_id
            """,
            ("receipt_id", approvedReceipt.Id));
        Assert.Equal(1_000_000m, heldAmount);
    }

    [Fact]
    public async Task ListHeldCredits_ByCustomer_ReturnsPagedItems()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var adminId = Guid.Parse("95555555-5555-5555-5555-555555555555");
        await SeedUserAsync(db, adminId, "admin-held-credit-list");
        var (seller, customer) = await SeedMasterAsync(db, adminId);

        var originalInvoice = await SeedPostedInvoiceAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            "INV-HOLD-LIST-001",
            1_000_000m);

        var receiptService = BuildReceiptService(db, adminId, ["Admin"]);
        var sourceReceipt = await CreateApprovedReceiptAsync(
            receiptService,
            seller.SellerTaxCode,
            customer.TaxCode,
            "PT-HOLD-LIST-001",
            new DateOnly(2026, 3, 6),
            1_000_000m,
            [new ReceiptTargetRef(originalInvoice.Id, "INVOICE")]);

        var invoiceService = BuildInvoiceService(db, adminId, ["Admin"]);
        await invoiceService.VoidAsync(
            originalInvoice.Id,
            new InvoiceVoidRequest(
                "Void original invoice to create held credit for listing",
                null,
                Force: true,
                Version: originalInvoice.Version),
            CancellationToken.None);

        var heldCreditService = BuildHeldCreditService(db, adminId, ["Admin"]);
        var result = await heldCreditService.ListByCustomerAsync(
            customer.TaxCode,
            new ReceiptHeldCreditListRequest(
                Status: null,
                Search: null,
                DocumentNo: null,
                ReceiptNo: null,
                From: null,
                To: null,
                Page: 1,
                PageSize: 20),
            CancellationToken.None);

        Assert.Equal(1, result.Total);
        var item = Assert.Single(result.Items);
        Assert.Equal(sourceReceipt.Id, item.ReceiptId);
        Assert.Equal("PT-HOLD-LIST-001", item.ReceiptNo);
        Assert.Equal(originalInvoice.Id, item.OriginalInvoiceId);
        Assert.Equal("INV-HOLD-LIST-001", item.OriginalInvoiceNo);
        Assert.Equal(1_000_000m, item.AmountRemaining);
        Assert.Equal(0m, item.AppliedAmount);
        Assert.Equal("HOLDING", item.Status);
    }

    [Fact]
    public async Task ApplyHeldCredit_WithGeneralCreditTopUp_UsesOtherCustomerReceiptsFifo()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var adminId = Guid.Parse("92222222-2222-2222-2222-222222222222");
        await SeedUserAsync(db, adminId, "admin-held-credit-topup");
        var (seller, customer) = await SeedMasterAsync(db, adminId);

        var originalInvoice = await SeedPostedInvoiceAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            "INV-HOLD-002",
            1_000_000m);

        var receiptService = BuildReceiptService(db, adminId, ["Admin"]);
        var sourceReceipt = await CreateApprovedReceiptAsync(
            receiptService,
            seller.SellerTaxCode,
            customer.TaxCode,
            "PT-HOLD-002",
            new DateOnly(2026, 3, 1),
            1_000_000m,
            [new ReceiptTargetRef(originalInvoice.Id, "INVOICE")]);

        var invoiceService = BuildInvoiceService(db, adminId, ["Admin"]);
        await invoiceService.VoidAsync(
            originalInvoice.Id,
            new InvoiceVoidRequest(
                "Void to create held credit before replacement",
                null,
                Force: true,
                Version: originalInvoice.Version),
            CancellationToken.None);

        var heldCredit = await db.ReceiptHeldCredits.SingleAsync(item =>
            item.ReceiptId == sourceReceipt.Id &&
            item.OriginalInvoiceId == originalInvoice.Id);

        var olderGeneralReceipt = await CreateApprovedReceiptAsync(
            receiptService,
            seller.SellerTaxCode,
            customer.TaxCode,
            "PT-GENERAL-OLD",
            new DateOnly(2026, 3, 2),
            100_000m,
            []);

        var newerGeneralReceipt = await CreateApprovedReceiptAsync(
            receiptService,
            seller.SellerTaxCode,
            customer.TaxCode,
            "PT-GENERAL-NEW",
            new DateOnly(2026, 3, 3),
            250_000m,
            []);

        var replacementInvoice = await SeedPostedInvoiceAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            "INV-HOLD-003",
            1_300_000m);

        var heldCreditService = BuildHeldCreditService(db, adminId, ["Admin"]);
        var applyResult = await heldCreditService.ApplyToInvoiceAsync(
            heldCredit.Id,
            new ReceiptHeldCreditApplyRequest(
                replacementInvoice.Id,
                UseGeneralCreditTopUp: true,
                Version: heldCredit.Version),
            CancellationToken.None);

        Assert.Equal(1_000_000m, applyResult.AppliedHeldAmount);
        Assert.Equal(300_000m, applyResult.AppliedGeneralCreditAmount);
        Assert.Equal(0m, applyResult.RemainingHeldAmount);
        Assert.Equal(0m, applyResult.InvoiceOutstandingAmount);
        Assert.Equal("REAPPLIED", applyResult.Status);

        var persistedHeldCredit = await db.ReceiptHeldCredits.AsNoTracking().SingleAsync(item => item.Id == heldCredit.Id);
        Assert.Equal(0m, persistedHeldCredit.AmountRemaining);
        Assert.Equal("REAPPLIED", persistedHeldCredit.Status);

        var persistedSourceReceipt = await db.Receipts.AsNoTracking().SingleAsync(item => item.Id == sourceReceipt.Id);
        Assert.Equal(0m, persistedSourceReceipt.UnallocatedAmount);
        Assert.Equal("ALLOCATED", persistedSourceReceipt.AllocationStatus);

        var persistedOlderReceipt = await db.Receipts.AsNoTracking().SingleAsync(item => item.Id == olderGeneralReceipt.Id);
        Assert.Equal(0m, persistedOlderReceipt.UnallocatedAmount);
        Assert.Equal("ALLOCATED", persistedOlderReceipt.AllocationStatus);

        var persistedNewerReceipt = await db.Receipts.AsNoTracking().SingleAsync(item => item.Id == newerGeneralReceipt.Id);
        Assert.Equal(50_000m, persistedNewerReceipt.UnallocatedAmount);
        Assert.Equal("PARTIAL", persistedNewerReceipt.AllocationStatus);

        var replacementInvoiceAfterApply = await db.Invoices.AsNoTracking().SingleAsync(item => item.Id == replacementInvoice.Id);
        Assert.Equal(0m, replacementInvoiceAfterApply.OutstandingAmount);
        Assert.Equal("PAID", replacementInvoiceAfterApply.Status);

        var replacementAllocations = await db.ReceiptAllocations
            .AsNoTracking()
            .Where(item => item.InvoiceId == replacementInvoice.Id)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync();

        Assert.Equal(3, replacementAllocations.Count);
        Assert.Contains(replacementAllocations, item => item.ReceiptId == sourceReceipt.Id && item.HeldCreditId == heldCredit.Id && item.Amount == 1_000_000m);
        Assert.Contains(replacementAllocations, item => item.ReceiptId == olderGeneralReceipt.Id && item.HeldCreditId == null && item.Amount == 100_000m);
        Assert.Contains(replacementAllocations, item => item.ReceiptId == newerGeneralReceipt.Id && item.HeldCreditId == null && item.Amount == 200_000m);
    }

    [Fact]
    public async Task ReleaseHeldCredit_MovesRemainingAmountBackToSourceReceipt()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var adminId = Guid.Parse("93333333-3333-3333-3333-333333333333");
        await SeedUserAsync(db, adminId, "admin-held-credit-release");
        var (seller, customer) = await SeedMasterAsync(db, adminId);

        var invoice = await SeedPostedInvoiceAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            "INV-HOLD-004",
            1_000_000m);

        var receiptService = BuildReceiptService(db, adminId, ["Admin"]);
        var sourceReceipt = await CreateApprovedReceiptAsync(
            receiptService,
            seller.SellerTaxCode,
            customer.TaxCode,
            "PT-HOLD-004",
            new DateOnly(2026, 3, 4),
            1_000_000m,
            [new ReceiptTargetRef(invoice.Id, "INVOICE")]);

        var invoiceService = BuildInvoiceService(db, adminId, ["Admin"]);
        await invoiceService.VoidAsync(
            invoice.Id,
            new InvoiceVoidRequest(
                "Void and release held credit",
                null,
                Force: true,
                Version: invoice.Version),
            CancellationToken.None);

        var heldCredit = await db.ReceiptHeldCredits.SingleAsync(item =>
            item.ReceiptId == sourceReceipt.Id &&
            item.OriginalInvoiceId == invoice.Id);
        var heldCreditService = BuildHeldCreditService(db, adminId, ["Admin"]);

        var releaseResult = await heldCreditService.ReleaseToGeneralCreditAsync(
            heldCredit.Id,
            new ReceiptHeldCreditReleaseRequest(heldCredit.Version),
            CancellationToken.None);

        Assert.Equal(1_000_000m, releaseResult.ReleasedAmount);
        Assert.Equal(0m, releaseResult.RemainingHeldAmount);
        Assert.Equal(1_000_000m, releaseResult.ReceiptUnallocatedAmount);
        Assert.Equal("RELEASED", releaseResult.Status);

        var persistedHeldCredit = await db.ReceiptHeldCredits.AsNoTracking().SingleAsync(item => item.Id == heldCredit.Id);
        Assert.Equal(0m, persistedHeldCredit.AmountRemaining);
        Assert.Equal("RELEASED", persistedHeldCredit.Status);

        var persistedSourceReceipt = await db.Receipts.AsNoTracking().SingleAsync(item => item.Id == sourceReceipt.Id);
        Assert.Equal(1_000_000m, persistedSourceReceipt.UnallocatedAmount);
        Assert.Equal("UNALLOCATED", persistedSourceReceipt.AllocationStatus);
    }

    [Fact]
    public async Task VoidReplacementInvoice_RestoresAppliedAmountBackToExistingHeldCredit()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var adminId = Guid.Parse("94444444-4444-4444-4444-444444444444");
        await SeedUserAsync(db, adminId, "admin-held-credit-restore");
        var (seller, customer) = await SeedMasterAsync(db, adminId);

        var originalInvoice = await SeedPostedInvoiceAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            "INV-HOLD-005",
            1_000_000m);

        var receiptService = BuildReceiptService(db, adminId, ["Admin"]);
        var sourceReceipt = await CreateApprovedReceiptAsync(
            receiptService,
            seller.SellerTaxCode,
            customer.TaxCode,
            "PT-HOLD-005",
            new DateOnly(2026, 3, 5),
            1_000_000m,
            [new ReceiptTargetRef(originalInvoice.Id, "INVOICE")]);

        var invoiceService = BuildInvoiceService(db, adminId, ["Admin"]);
        await invoiceService.VoidAsync(
            originalInvoice.Id,
            new InvoiceVoidRequest(
                "Void original invoice to create held credit",
                null,
                Force: true,
                Version: originalInvoice.Version),
            CancellationToken.None);

        var heldCredit = await db.ReceiptHeldCredits.SingleAsync(item =>
            item.ReceiptId == sourceReceipt.Id &&
            item.OriginalInvoiceId == originalInvoice.Id);
        var replacementInvoice = await SeedPostedInvoiceAsync(
            db,
            seller.SellerTaxCode,
            customer.TaxCode,
            "INV-HOLD-006",
            600_000m);

        var heldCreditService = BuildHeldCreditService(db, adminId, ["Admin"]);
        var applyResult = await heldCreditService.ApplyToInvoiceAsync(
            heldCredit.Id,
            new ReceiptHeldCreditApplyRequest(
                replacementInvoice.Id,
                UseGeneralCreditTopUp: false,
                Version: heldCredit.Version),
            CancellationToken.None);

        Assert.Equal(600_000m, applyResult.AppliedHeldAmount);
        Assert.Equal(400_000m, applyResult.RemainingHeldAmount);
        Assert.Equal("PARTIAL", applyResult.Status);

        var replacementInvoiceAfterApply = await db.Invoices.AsNoTracking().SingleAsync(item => item.Id == replacementInvoice.Id);
        var voidReplacementResult = await invoiceService.VoidAsync(
            replacementInvoice.Id,
            new InvoiceVoidRequest(
                "Void replacement invoice and restore held credit",
                null,
                Force: true,
                Version: replacementInvoiceAfterApply.Version),
            CancellationToken.None);

        Assert.Equal(0m, voidReplacementResult.HeldCreditAmount);
        Assert.Equal(0, voidReplacementResult.HeldCreditCount);
        Assert.Equal(600_000m, voidReplacementResult.RestoredHeldCreditAmount);
        Assert.Equal(1, voidReplacementResult.RestoredHeldCreditCount);

        var heldCredits = await db.ReceiptHeldCredits.AsNoTracking().ToListAsync();
        var restoredHeldCredit = Assert.Single(heldCredits);
        Assert.Equal(heldCredit.Id, restoredHeldCredit.Id);
        Assert.Equal(1_000_000m, restoredHeldCredit.AmountRemaining);
        Assert.Equal("HOLDING", restoredHeldCredit.Status);

        var replacementAllocations = await db.ReceiptAllocations
            .AsNoTracking()
            .Where(item => item.InvoiceId == replacementInvoice.Id)
            .CountAsync();
        Assert.Equal(0, replacementAllocations);
    }

    private static ReceiptService BuildReceiptService(
        ConGNoDbContext db,
        Guid userId,
        IReadOnlyList<string> roles)
    {
        var currentUser = new TestCurrentUser(userId, roles);
        return new ReceiptService(db, currentUser, new AuditService(db, currentUser));
    }

    private static ReceiptHeldCreditService BuildHeldCreditService(
        ConGNoDbContext db,
        Guid userId,
        IReadOnlyList<string> roles)
    {
        var currentUser = new TestCurrentUser(userId, roles);
        return new ReceiptHeldCreditService(db, currentUser, new AuditService(db, currentUser));
    }

    private static InvoiceService BuildInvoiceService(
        ConGNoDbContext db,
        Guid userId,
        IReadOnlyList<string> roles)
    {
        var currentUser = new TestCurrentUser(userId, roles);
        return new InvoiceService(db, currentUser, new AuditService(db, currentUser));
    }

    private static async Task<ReceiptDto> CreateApprovedReceiptAsync(
        ReceiptService receiptService,
        string sellerTaxCode,
        string customerTaxCode,
        string receiptNo,
        DateOnly receiptDate,
        decimal amount,
        IReadOnlyList<ReceiptTargetRef>? selectedTargets)
    {
        var draftReceipt = await receiptService.CreateAsync(
            new ReceiptCreateRequest(
                sellerTaxCode,
                customerTaxCode,
                receiptNo,
                receiptDate,
                amount,
                "MANUAL",
                null,
                "BANK",
                "Integration test receipt",
                "ISSUE_DATE",
                selectedTargets),
            CancellationToken.None);

        await receiptService.ApproveAsync(
            draftReceipt.Id,
            new ReceiptApproveRequest(null, draftReceipt.Version),
            CancellationToken.None);

        return draftReceipt;
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
            SellerTaxCode = "SELLER-HOLD",
            Name = "Seller Hold",
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        var customer = new Customer
        {
            TaxCode = "CUST-HOLD",
            Name = "Customer Hold",
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

    private static async Task<Invoice> SeedPostedInvoiceAsync(
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

        var customer = await db.Customers.FirstAsync(c => c.TaxCode == customerTaxCode);
        customer.CurrentBalance += amount;

        await db.SaveChangesAsync();
        return invoice;
    }

    private static async Task<T> ExecuteScalarAsync<T>(
        ConGNoDbContext db,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }

            var result = await command.ExecuteScalarAsync();
            return (T)Convert.ChangeType(result!, typeof(T));
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
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
