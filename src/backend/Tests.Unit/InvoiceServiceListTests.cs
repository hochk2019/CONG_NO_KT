using System.Collections;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Invoices;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests.Unit;

public sealed class InvoiceServiceListTests
{
    [Fact]
    public async Task ListAsync_ReturnsRootInvoiceRows_WithCustomerSellerAndReferenceMappings()
    {
        await using var db = CreateDbContext(nameof(ListAsync_ReturnsRootInvoiceRows_WithCustomerSellerAndReferenceMappings));
        var now = new DateTimeOffset(2026, 4, 9, 1, 0, 0, TimeSpan.Zero);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var receiptId = Guid.NewGuid();
        var openInvoiceId = Guid.NewGuid();
        var reductionInvoiceId = Guid.NewGuid();
        var olderInvoiceId = Guid.NewGuid();

        db.Sellers.Add(new Seller
        {
            SellerTaxCode = "SELLER-INV",
            Name = "Seller Invoice",
            ShortName = "Seller INV",
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        });

        db.Customers.AddRange(
            new Customer
            {
                TaxCode = "CUST-A",
                Name = "Customer Alpha",
                Status = "ACTIVE",
                CurrentBalance = 0m,
                PaymentTermsDays = 30,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            },
            new Customer
            {
                TaxCode = "CUST-B",
                Name = "Customer Beta",
                Status = "ACTIVE",
                CurrentBalance = 0m,
                PaymentTermsDays = 30,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            });

        db.Invoices.AddRange(
            new Invoice
            {
                Id = openInvoiceId,
                SellerTaxCode = "SELLER-INV",
                CustomerTaxCode = "CUST-A",
                InvoiceNo = "INV-OPEN-ROOT",
                IssueDate = today,
                RevenueExclVat = 909.09m,
                VatAmount = 90.91m,
                TotalAmount = 1_000m,
                OutstandingAmount = 700m,
                InvoiceType = "NORMAL",
                Status = "OPEN",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 2
            },
            new Invoice
            {
                Id = reductionInvoiceId,
                SellerTaxCode = "SELLER-INV",
                CustomerTaxCode = "CUST-A",
                InvoiceNo = "INV-RED-ROOT",
                IssueDate = today.AddDays(-1),
                RevenueExclVat = -181.82m,
                VatAmount = -18.18m,
                TotalAmount = -200m,
                OutstandingAmount = 0m,
                InvoiceType = "ADJUSTMENT_REDUCTION",
                Status = "PAID",
                CreatedAt = now.AddMinutes(-5),
                UpdatedAt = now.AddMinutes(-5),
                Version = 1
            },
            new Invoice
            {
                Id = olderInvoiceId,
                SellerTaxCode = "SELLER-INV",
                CustomerTaxCode = "CUST-B",
                InvoiceNo = "INV-OLDER-ROOT",
                IssueDate = today.AddDays(-10),
                RevenueExclVat = 454.55m,
                VatAmount = 45.45m,
                TotalAmount = 500m,
                OutstandingAmount = 500m,
                InvoiceType = "NORMAL",
                Status = "OPEN",
                CreatedAt = now.AddMinutes(-10),
                UpdatedAt = now.AddMinutes(-10),
                Version = 0
            });

        db.Receipts.Add(new Receipt
        {
            Id = receiptId,
            ReceiptNo = "PT-0001",
            ReceiptDate = today,
            CustomerTaxCode = "CUST-A",
            Amount = 300m,
            Status = "BOOKED",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        });

        db.ReceiptAllocations.Add(new ReceiptAllocation
        {
            Id = Guid.NewGuid(),
            ReceiptId = receiptId,
            InvoiceId = openInvoiceId,
            Amount = 300m,
            CreatedAt = now
        });

        db.InvoiceReductionApplications.Add(new InvoiceReductionApplication
        {
            Id = Guid.NewGuid(),
            ReductionInvoiceId = reductionInvoiceId,
            AppliedInvoiceId = openInvoiceId,
            Amount = 200m,
            CreatedAt = now
        });

        await db.SaveChangesAsync();

        var service = new InvoiceService(db, new StubCurrentUser(), new StubAuditService());
        var result = await service.ListAsync(
            new InvoiceListRequest(
                "OPEN",
                null,
                null,
                null,
                today.AddDays(-30),
                today,
                1,
                20),
            CancellationToken.None);

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("INV-OPEN-ROOT", result.Items[0].InvoiceNo);
        Assert.Equal("Customer Alpha", result.Items[0].CustomerName);
        Assert.Equal("Seller INV", result.Items[0].SellerShortName);

        var receiptRef = Assert.Single(result.Items[0].ReceiptRefs);
        Assert.Equal("PT-0001", receiptRef.ReceiptNo);
        Assert.Equal(300m, receiptRef.Amount);

        var reductionRef = Assert.Single(result.Items[0].ReductionInvoiceRefs);
        Assert.Equal("INV-RED-ROOT", reductionRef.InvoiceNo);
        Assert.Equal(200m, reductionRef.Amount);

        var olderInvoice = result.Items.Single(item => item.InvoiceNo == "INV-OLDER-ROOT");
        Assert.Equal("Customer Beta", olderInvoice.CustomerName);
        Assert.Empty(olderInvoice.ReceiptRefs);
        Assert.Empty(olderInvoice.ReductionInvoiceRefs);
        Assert.Empty(olderInvoice.ReducedInvoiceRefs);
    }

    private static ConGNoDbContext CreateDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"invoice-service-list-{name}")
            .Options;

        return new ConGNoDbContext(options);
    }

    private sealed class StubCurrentUser : ICurrentUser
    {
        public Guid? UserId => Guid.NewGuid();
        public string? Username => "tester";
        public IReadOnlyList<string> Roles => ["Viewer"];
        public IReadOnlyList<string> Permissions => [];
        public string? IpAddress => "127.0.0.1";
    }

    private sealed class StubAuditService : IAuditService
    {
        public Task LogAsync(string action, string entityType, string entityId, object? before, object? after, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
