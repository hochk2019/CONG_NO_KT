using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests.Unit;

public sealed class CustomerService360Tests
{
    [Fact]
    public async Task Get360Async_ReturnsComputedSummary_AndLatestSignals()
    {
        await using var db = CreateDbContext(nameof(Get360Async_ReturnsComputedSummary_AndLatestSignals));
        var now = new DateTimeOffset(2026, 2, 27, 10, 0, 0, TimeSpan.Zero);
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var ownerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        db.Users.AddRange(
            new User
            {
                Id = ownerId,
                Username = "owner.user",
                FullName = null,
                PasswordHash = "x",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            },
            new User
            {
                Id = managerId,
                Username = "manager.user",
                FullName = "Manager Name",
                PasswordHash = "x",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            });

        AddActiveSeller(db, now, "SELLER-360");
        AddActiveCustomer(db, now, "CUST-360", currentBalance: 200m, paymentTermsDays: 30, creditLimit: 1_000m, ownerId, managerId);

        db.Invoices.AddRange(
            CreateOpenInvoice("SELLER-360", "CUST-360", "INV-OVERDUE", today.AddDays(-45), 200m, now),
            CreateOpenInvoice("SELLER-360", "CUST-360", "INV-UPCOMING", today.AddDays(-10), 50m, now),
            new Invoice
            {
                Id = Guid.NewGuid(),
                SellerTaxCode = "SELLER-360",
                CustomerTaxCode = "CUST-360",
                InvoiceNo = "INV-VOID",
                IssueDate = today.AddDays(-60),
                RevenueExclVat = 81.82m,
                VatAmount = 8.18m,
                TotalAmount = 90m,
                OutstandingAmount = 90m,
                InvoiceType = "NORMAL",
                Status = "VOID",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            });

        db.Advances.Add(CreateAdvance("SELLER-360", "CUST-360", "TH-001", today.AddDays(-5), 40m, "APPROVED", now));

        var freeReceipt = CreateApprovedReceipt(
            "SELLER-360",
            "CUST-360",
            "RCPT-FREE",
            today.AddDays(-3),
            amount: 60m,
            unallocatedAmount: 60m,
            allocationStatus: "UNALLOCATED",
            now);
        var heldReceipt = CreateApprovedReceipt(
            "SELLER-360",
            "CUST-360",
            "RCPT-HELD",
            today.AddDays(-2),
            amount: 30m,
            unallocatedAmount: 0m,
            allocationStatus: "PARTIAL",
            now);

        db.Receipts.AddRange(freeReceipt, heldReceipt);
        db.ReceiptHeldCredits.Add(new ReceiptHeldCredit
        {
            Id = Guid.NewGuid(),
            ReceiptId = heldReceipt.Id,
            OriginalInvoiceId = Guid.NewGuid(),
            OriginalAmount = 30m,
            AmountRemaining = 30m,
            Status = "HOLDING",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        });

        db.RiskScoreSnapshots.AddRange(
            new RiskScoreSnapshot
            {
                Id = Guid.NewGuid(),
                CustomerTaxCode = "CUST-360",
                AsOfDate = today.AddDays(-2),
                Score = 0.41m,
                Signal = "MEDIUM",
                ModelVersion = "v1",
                CreatedAt = now.AddDays(-2)
            },
            new RiskScoreSnapshot
            {
                Id = Guid.NewGuid(),
                CustomerTaxCode = "CUST-360",
                AsOfDate = today.AddDays(-1),
                Score = 0.72m,
                Signal = "HIGH",
                ModelVersion = "v2",
                CreatedAt = now.AddDays(-1)
            });

        db.ReminderLogs.AddRange(
            new ReminderLog
            {
                Id = Guid.NewGuid(),
                CustomerTaxCode = "CUST-360",
                Channel = "EMAIL",
                Status = "SENT",
                RiskLevel = "MEDIUM",
                EscalationLevel = 1,
                SentAt = now.AddHours(-3),
                CreatedAt = now.AddHours(-3)
            },
            new ReminderLog
            {
                Id = Guid.NewGuid(),
                CustomerTaxCode = "CUST-360",
                Channel = "ZALO",
                Status = "FAILED",
                RiskLevel = "HIGH",
                EscalationLevel = 2,
                EscalationReason = "NO_RESPONSE",
                SentAt = now.AddHours(-1),
                CreatedAt = now.AddHours(-1)
            });

        db.ReminderResponseStates.AddRange(
            new ReminderResponseState
            {
                Id = Guid.NewGuid(),
                CustomerTaxCode = "CUST-360",
                Channel = "ZALO",
                ResponseStatus = "NO_RESPONSE",
                EscalationLocked = false,
                AttemptCount = 2,
                CurrentEscalationLevel = 2,
                LastSentAt = now.AddHours(-1),
                CreatedAt = now.AddHours(-4),
                UpdatedAt = now.AddHours(-1)
            },
            new ReminderResponseState
            {
                Id = Guid.NewGuid(),
                CustomerTaxCode = "CUST-360",
                Channel = "EMAIL",
                ResponseStatus = "RESPONDED",
                LatestResponseAt = now.AddMinutes(-30),
                EscalationLocked = true,
                AttemptCount = 1,
                CurrentEscalationLevel = 1,
                LastSentAt = now.AddHours(-3),
                CreatedAt = now.AddHours(-5),
                UpdatedAt = now.AddMinutes(-30)
            });

        await db.SaveChangesAsync();

        var service = new CustomerService(db, () => now.UtcDateTime);
        var result = await service.Get360Async(" CUST-360 ", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("owner.user", result!.OwnerName);
        Assert.Equal("Manager Name", result.ManagerName);
        Assert.Equal(200m, result.Summary.TotalOutstanding);
        Assert.Equal(200m, result.Summary.NetPosition);
        Assert.Equal(250m, result.Summary.InvoiceOutstanding);
        Assert.Equal(40m, result.Summary.AdvanceOutstanding);
        Assert.Equal(290m, result.Summary.OpenOutstanding);
        Assert.Equal(90m, result.Summary.UnallocatedCredit);
        Assert.Equal(-90m, result.Summary.NetAdjustment);
        Assert.Equal(200m, result.Summary.OverdueAmount);
        Assert.Equal(0.8m, result.Summary.OverdueRatio);
        Assert.Equal(15, result.Summary.MaxDaysPastDue);
        Assert.Equal(2, result.Summary.OpenInvoiceCount);
        Assert.Equal(today.AddDays(20), result.Summary.NextDueDate);

        Assert.Equal(0.72m, result.RiskSnapshot.Score);
        Assert.Equal("HIGH", result.RiskSnapshot.Signal);
        Assert.Equal("v2", result.RiskSnapshot.ModelVersion);
        Assert.Equal(today.AddDays(-1), result.RiskSnapshot.AsOfDate);

        Assert.Equal(2, result.ReminderTimeline.Count);
        Assert.Equal("ZALO", result.ReminderTimeline[0].Channel);
        Assert.Equal("EMAIL", result.ReminderTimeline[1].Channel);

        Assert.Equal(2, result.ResponseStates.Count);
        Assert.Equal("EMAIL", result.ResponseStates[0].Channel);
        Assert.Equal("ZALO", result.ResponseStates[1].Channel);
    }

    [Fact]
    public async Task Get360Async_Summary_WithOnlyOpenInvoices_HasNoUnallocatedCredit()
    {
        await using var db = CreateDbContext(nameof(Get360Async_Summary_WithOnlyOpenInvoices_HasNoUnallocatedCredit));
        var now = new DateTimeOffset(2026, 2, 27, 10, 0, 0, TimeSpan.Zero);
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        AddActiveSeller(db, now, "SELLER-ONLY-INVOICE");
        AddActiveCustomer(db, now, "CUST-ONLY-INVOICE", currentBalance: 300m, paymentTermsDays: 30);
        db.Invoices.Add(CreateOpenInvoice("SELLER-ONLY-INVOICE", "CUST-ONLY-INVOICE", "INV-001", today.AddDays(-7), 300m, now));

        await db.SaveChangesAsync();

        var result = await new CustomerService(db, () => now.UtcDateTime)
            .Get360Async("CUST-ONLY-INVOICE", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(300m, result!.Summary.TotalOutstanding);
        Assert.Equal(300m, result.Summary.NetPosition);
        Assert.Equal(300m, result.Summary.InvoiceOutstanding);
        Assert.Equal(0m, result.Summary.AdvanceOutstanding);
        Assert.Equal(300m, result.Summary.OpenOutstanding);
        Assert.Equal(0m, result.Summary.UnallocatedCredit);
        Assert.Equal(0m, result.Summary.NetAdjustment);
    }

    [Fact]
    public async Task Get360Async_Summary_WithPartialUnallocatedReceipt_ShowsDebtAndCreditTogether()
    {
        await using var db = CreateDbContext(nameof(Get360Async_Summary_WithPartialUnallocatedReceipt_ShowsDebtAndCreditTogether));
        var now = new DateTimeOffset(2026, 2, 27, 10, 0, 0, TimeSpan.Zero);
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        AddActiveSeller(db, now, "SELLER-PARTIAL");
        AddActiveCustomer(db, now, "CUST-PARTIAL", currentBalance: 200m, paymentTermsDays: 30);
        db.Invoices.Add(CreateOpenInvoice("SELLER-PARTIAL", "CUST-PARTIAL", "INV-001", today.AddDays(-7), 250m, now));
        db.Receipts.Add(CreateApprovedReceipt(
            "SELLER-PARTIAL",
            "CUST-PARTIAL",
            "RCPT-001",
            today.AddDays(-3),
            amount: 50m,
            unallocatedAmount: 20m,
            allocationStatus: "PARTIAL",
            now));

        await db.SaveChangesAsync();

        var result = await new CustomerService(db, () => now.UtcDateTime)
            .Get360Async("CUST-PARTIAL", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(200m, result!.Summary.NetPosition);
        Assert.Equal(250m, result.Summary.OpenOutstanding);
        Assert.Equal(20m, result.Summary.UnallocatedCredit);
    }

    [Fact]
    public async Task Get360Async_Summary_WithOnlyUnallocatedReceipt_ShowsNegativeNetPosition()
    {
        await using var db = CreateDbContext(nameof(Get360Async_Summary_WithOnlyUnallocatedReceipt_ShowsNegativeNetPosition));
        var now = new DateTimeOffset(2026, 2, 27, 10, 0, 0, TimeSpan.Zero);
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        AddActiveSeller(db, now, "SELLER-CREDIT");
        AddActiveCustomer(db, now, "CUST-CREDIT", currentBalance: -120m, paymentTermsDays: 30);
        db.Receipts.Add(CreateApprovedReceipt(
            "SELLER-CREDIT",
            "CUST-CREDIT",
            "RCPT-ONLY",
            today.AddDays(-1),
            amount: 120m,
            unallocatedAmount: 120m,
            allocationStatus: "UNALLOCATED",
            now));

        await db.SaveChangesAsync();

        var result = await new CustomerService(db, () => now.UtcDateTime)
            .Get360Async("CUST-CREDIT", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0m, result!.Summary.OpenOutstanding);
        Assert.Equal(120m, result.Summary.UnallocatedCredit);
        Assert.Equal(-120m, result.Summary.NetPosition);
        Assert.Equal(-120m, result.Summary.TotalOutstanding);
    }

    [Fact]
    public async Task Get360Async_Summary_WithOpenOutstanding_AndCredit_CanNetToZero()
    {
        await using var db = CreateDbContext(nameof(Get360Async_Summary_WithOpenOutstanding_AndCredit_CanNetToZero));
        var now = new DateTimeOffset(2026, 2, 27, 10, 0, 0, TimeSpan.Zero);
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        AddActiveSeller(db, now, "SELLER-NET-ZERO");
        AddActiveCustomer(db, now, "CUST-NET-ZERO", currentBalance: 0m, paymentTermsDays: 30);

        db.Invoices.Add(CreateOpenInvoice("SELLER-NET-ZERO", "CUST-NET-ZERO", "INV-001", today.AddDays(-4), 180m, now));
        db.Advances.Add(CreateAdvance("SELLER-NET-ZERO", "CUST-NET-ZERO", "TH-001", today.AddDays(-4), 40m, "APPROVED", now));

        var receipt = CreateApprovedReceipt(
            "SELLER-NET-ZERO",
            "CUST-NET-ZERO",
            "RCPT-FREE",
            today.AddDays(-2),
            amount: 170m,
            unallocatedAmount: 170m,
            allocationStatus: "UNALLOCATED",
            now);
        var heldReceipt = CreateApprovedReceipt(
            "SELLER-NET-ZERO",
            "CUST-NET-ZERO",
            "RCPT-HELD",
            today.AddDays(-1),
            amount: 50m,
            unallocatedAmount: 0m,
            allocationStatus: "PARTIAL",
            now);

        db.Receipts.AddRange(receipt, heldReceipt);
        db.ReceiptHeldCredits.Add(new ReceiptHeldCredit
        {
            Id = Guid.NewGuid(),
            ReceiptId = heldReceipt.Id,
            OriginalInvoiceId = Guid.NewGuid(),
            OriginalAmount = 50m,
            AmountRemaining = 50m,
            Status = "HOLDING",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        });

        await db.SaveChangesAsync();

        var result = await new CustomerService(db, () => now.UtcDateTime)
            .Get360Async("CUST-NET-ZERO", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(220m, result!.Summary.OpenOutstanding);
        Assert.Equal(220m, result.Summary.UnallocatedCredit);
        Assert.Equal(0m, result.Summary.NetPosition);
        Assert.Equal(0m, result.Summary.TotalOutstanding);
    }

    [Fact]
    public async Task Get360Async_ReturnsNull_WhenCustomerDoesNotExist()
    {
        await using var db = CreateDbContext(nameof(Get360Async_ReturnsNull_WhenCustomerDoesNotExist));
        var service = new CustomerService(db);

        var result = await service.Get360Async("NOT-FOUND", CancellationToken.None);

        Assert.Null(result);
    }

    private static void AddActiveSeller(ConGNoDbContext db, DateTimeOffset now, string sellerTaxCode)
    {
        db.Sellers.Add(new Seller
        {
            SellerTaxCode = sellerTaxCode,
            Name = sellerTaxCode,
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        });
    }

    private static void AddActiveCustomer(
        ConGNoDbContext db,
        DateTimeOffset now,
        string taxCode,
        decimal currentBalance,
        int paymentTermsDays,
        decimal? creditLimit = null,
        Guid? ownerId = null,
        Guid? managerId = null)
    {
        db.Customers.Add(new Customer
        {
            TaxCode = taxCode,
            Name = taxCode,
            Status = "ACTIVE",
            CurrentBalance = currentBalance,
            PaymentTermsDays = paymentTermsDays,
            CreditLimit = creditLimit,
            AccountantOwnerId = ownerId,
            ManagerUserId = managerId,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        });
    }

    private static Invoice CreateOpenInvoice(
        string sellerTaxCode,
        string customerTaxCode,
        string invoiceNo,
        DateOnly issueDate,
        decimal outstandingAmount,
        DateTimeOffset now)
    {
        return new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            InvoiceNo = invoiceNo,
            IssueDate = issueDate,
            RevenueExclVat = outstandingAmount,
            VatAmount = 0m,
            TotalAmount = outstandingAmount,
            OutstandingAmount = outstandingAmount,
            InvoiceType = "NORMAL",
            Status = "OPEN",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        };
    }

    private static Advance CreateAdvance(
        string sellerTaxCode,
        string customerTaxCode,
        string advanceNo,
        DateOnly advanceDate,
        decimal amount,
        string status,
        DateTimeOffset now)
    {
        return new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            AdvanceNo = advanceNo,
            AdvanceDate = advanceDate,
            Amount = amount,
            OutstandingAmount = amount,
            Description = advanceNo,
            Status = status,
            ApprovedAt = status == "APPROVED" ? now : null,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        };
    }

    private static Receipt CreateApprovedReceipt(
        string sellerTaxCode,
        string customerTaxCode,
        string receiptNo,
        DateOnly receiptDate,
        decimal amount,
        decimal unallocatedAmount,
        string allocationStatus,
        DateTimeOffset now)
    {
        return new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            ReceiptNo = receiptNo,
            ReceiptDate = receiptDate,
            Amount = amount,
            Method = "BANK",
            Description = receiptNo,
            AllocationMode = "FIFO",
            AllocationStatus = allocationStatus,
            UnallocatedAmount = unallocatedAmount,
            Status = "APPROVED",
            ApprovedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        };
    }

    private static ConGNoDbContext CreateDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"customer-360-{name}")
            .Options;

        return new ConGNoDbContext(options);
    }
}
