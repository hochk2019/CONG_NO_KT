using CongNoGolden.Application.Customers;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests.Unit;

public sealed class CustomerServiceListTests
{
    [Theory]
    [InlineData("balance_asc", new[] { "CUST-B", "CUST-C", "CUST-A", "CUST-D" })]
    [InlineData("balance_desc", new[] { "CUST-D", "CUST-A", "CUST-C", "CUST-B" })]
    [InlineData("debt_oldest", new[] { "CUST-A", "CUST-B", "CUST-C", "CUST-D" })]
    [InlineData("debt_newest", new[] { "CUST-C", "CUST-B", "CUST-A", "CUST-D" })]
    public async Task ListAsync_AppliesRequestedSort(string sort, string[] expectedOrder)
    {
        await using var db = CreateDbContext(nameof(ListAsync_AppliesRequestedSort) + "-" + sort);
        var now = new DateTimeOffset(2026, 4, 7, 9, 0, 0, TimeSpan.Zero);
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        db.Customers.AddRange(
            new Customer
            {
                TaxCode = "CUST-A",
                Name = "Alpha Co",
                Status = "ACTIVE",
                CurrentBalance = 700m,
                PaymentTermsDays = 30,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            },
            new Customer
            {
                TaxCode = "CUST-B",
                Name = "Beta Co",
                Status = "ACTIVE",
                CurrentBalance = 100m,
                PaymentTermsDays = 30,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            },
            new Customer
            {
                TaxCode = "CUST-C",
                Name = "Gamma Co",
                Status = "ACTIVE",
                CurrentBalance = 450m,
                PaymentTermsDays = 30,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            },
            new Customer
            {
                TaxCode = "CUST-D",
                Name = "Delta Co",
                Status = "ACTIVE",
                CurrentBalance = 900m,
                PaymentTermsDays = 30,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            });

        db.Invoices.AddRange(
            new Invoice
            {
                Id = Guid.NewGuid(),
                SellerTaxCode = "SELLER-1",
                CustomerTaxCode = "CUST-A",
                InvoiceNo = "INV-A",
                IssueDate = today.AddDays(-40),
                RevenueExclVat = 636.36m,
                VatAmount = 63.64m,
                TotalAmount = 700m,
                OutstandingAmount = 700m,
                InvoiceType = "NORMAL",
                Status = "OPEN",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            },
            new Invoice
            {
                Id = Guid.NewGuid(),
                SellerTaxCode = "SELLER-1",
                CustomerTaxCode = "CUST-B",
                InvoiceNo = "INV-B",
                IssueDate = today.AddDays(-20),
                RevenueExclVat = 90.91m,
                VatAmount = 9.09m,
                TotalAmount = 100m,
                OutstandingAmount = 100m,
                InvoiceType = "NORMAL",
                Status = "OPEN",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            },
            new Invoice
            {
                Id = Guid.NewGuid(),
                SellerTaxCode = "SELLER-1",
                CustomerTaxCode = "CUST-C",
                InvoiceNo = "INV-C",
                IssueDate = today.AddDays(-5),
                RevenueExclVat = 409.09m,
                VatAmount = 40.91m,
                TotalAmount = 450m,
                OutstandingAmount = 450m,
                InvoiceType = "NORMAL",
                Status = "OPEN",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            },
            new Invoice
            {
                Id = Guid.NewGuid(),
                SellerTaxCode = "SELLER-1",
                CustomerTaxCode = "CUST-D",
                InvoiceNo = "INV-D-VOID",
                IssueDate = today.AddDays(-60),
                RevenueExclVat = 818.18m,
                VatAmount = 81.82m,
                TotalAmount = 900m,
                OutstandingAmount = 900m,
                InvoiceType = "NORMAL",
                Status = "VOID",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            });

        await db.SaveChangesAsync();

        var service = new CustomerService(db);
        var result = await service.ListAsync(
            new CustomerListRequest(
                Search: null,
                OwnerId: null,
                Status: null,
                Sort: sort,
                Page: 1,
                PageSize: 10),
            CancellationToken.None);

        Assert.Equal(expectedOrder, result.Items.Select(item => item.TaxCode).ToArray());
    }

    private static ConGNoDbContext CreateDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"customer-list-{name}")
            .Options;

        return new ConGNoDbContext(options);
    }
}
