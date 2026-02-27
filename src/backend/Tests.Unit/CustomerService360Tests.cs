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

        db.Sellers.Add(new Seller
        {
            SellerTaxCode = "SELLER-360",
            Name = "Seller 360",
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        });

        db.Customers.Add(new Customer
        {
            TaxCode = "CUST-360",
            Name = "Customer 360",
            Status = "ACTIVE",
            CurrentBalance = 300m,
            PaymentTermsDays = 30,
            CreditLimit = 1_000m,
            AccountantOwnerId = ownerId,
            ManagerUserId = managerId,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        });

        db.Invoices.AddRange(
            new Invoice
            {
                Id = Guid.NewGuid(),
                SellerTaxCode = "SELLER-360",
                CustomerTaxCode = "CUST-360",
                InvoiceNo = "INV-OVERDUE",
                IssueDate = today.AddDays(-45),
                RevenueExclVat = 181.82m,
                VatAmount = 18.18m,
                TotalAmount = 200m,
                OutstandingAmount = 200m,
                InvoiceType = "NORMAL",
                Status = "APPROVED",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            },
            new Invoice
            {
                Id = Guid.NewGuid(),
                SellerTaxCode = "SELLER-360",
                CustomerTaxCode = "CUST-360",
                InvoiceNo = "INV-UPCOMING",
                IssueDate = today.AddDays(-10),
                RevenueExclVat = 90.91m,
                VatAmount = 9.09m,
                TotalAmount = 100m,
                OutstandingAmount = 100m,
                InvoiceType = "NORMAL",
                Status = "APPROVED",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            },
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

        var service = new CustomerService(db);
        var result = await service.Get360Async(" CUST-360 ", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("owner.user", result!.OwnerName);
        Assert.Equal("Manager Name", result.ManagerName);
        Assert.Equal(300m, result.Summary.TotalOutstanding);
        Assert.Equal(200m, result.Summary.OverdueAmount);
        Assert.Equal(2m / 3m, result.Summary.OverdueRatio);
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
    public async Task Get360Async_ReturnsNull_WhenCustomerDoesNotExist()
    {
        await using var db = CreateDbContext(nameof(Get360Async_ReturnsNull_WhenCustomerDoesNotExist));
        var service = new CustomerService(db);

        var result = await service.Get360Async("NOT-FOUND", CancellationToken.None);

        Assert.Null(result);
    }

    private static ConGNoDbContext CreateDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"customer-360-{name}")
            .Options;

        return new ConGNoDbContext(options);
    }
}
