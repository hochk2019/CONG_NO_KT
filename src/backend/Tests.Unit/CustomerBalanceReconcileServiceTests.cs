using CongNoGolden.Application.Customers;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests.Unit;

public sealed class CustomerBalanceReconcileServiceTests
{
    [Fact]
    public async Task ReconcileAsync_DryRun_DetectsDriftWithoutMutatingBalances()
    {
        await using var db = CreateDbContext(nameof(ReconcileAsync_DryRun_DetectsDriftWithoutMutatingBalances));
        await SeedAsync(db);
        var service = new CustomerBalanceReconcileService(db);

        var result = await service.ReconcileAsync(
            new CustomerBalanceReconcileRequest(ApplyChanges: false, MaxItems: 5, Tolerance: 0.01m),
            CancellationToken.None);

        Assert.Equal(2, result.CheckedCustomers);
        Assert.Equal(1, result.DriftedCustomers);
        Assert.Equal(0, result.UpdatedCustomers);
        Assert.Single(result.TopDrifts);
        Assert.Equal("CUST-001", result.TopDrifts[0].TaxCode);
        Assert.Equal(999m, result.TopDrifts[0].CurrentBalance);
        Assert.Equal(110m, result.TopDrifts[0].ExpectedBalance);
        Assert.Equal(889m, result.TopDrifts[0].AbsoluteDrift);

        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.TaxCode == "CUST-001");
        Assert.Equal(999m, customer.CurrentBalance);
    }

    [Fact]
    public async Task ReconcileAsync_ApplyChanges_UpdatesDriftedCustomers()
    {
        await using var db = CreateDbContext(nameof(ReconcileAsync_ApplyChanges_UpdatesDriftedCustomers));
        await SeedAsync(db);
        var service = new CustomerBalanceReconcileService(db);

        var result = await service.ReconcileAsync(
            new CustomerBalanceReconcileRequest(ApplyChanges: true, MaxItems: 5, Tolerance: 0.01m),
            CancellationToken.None);

        Assert.Equal(1, result.DriftedCustomers);
        Assert.Equal(1, result.UpdatedCustomers);

        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.TaxCode == "CUST-001");
        Assert.Equal(110m, customer.CurrentBalance);
        Assert.Equal(1, customer.Version);

        var secondRun = await service.ReconcileAsync(
            new CustomerBalanceReconcileRequest(ApplyChanges: false, MaxItems: 5, Tolerance: 0.01m),
            CancellationToken.None);
        Assert.Equal(0, secondRun.DriftedCustomers);
    }

    [Fact]
    public async Task ReconcileAsync_ApplyChanges_LargeDataset_BoundsTrackedEntities()
    {
        await using var db = CreateDbContext(nameof(ReconcileAsync_ApplyChanges_LargeDataset_BoundsTrackedEntities));
        await SeedLargeCustomersAsync(db, customerCount: 1200);
        var service = new CustomerBalanceReconcileService(db);

        var result = await service.ReconcileAsync(
            new CustomerBalanceReconcileRequest(ApplyChanges: true, MaxItems: 10, Tolerance: 0.01m),
            CancellationToken.None);

        Assert.Equal(1200, result.CheckedCustomers);
        Assert.Equal(1200, result.DriftedCustomers);
        Assert.Equal(1200, result.UpdatedCustomers);
        Assert.Equal(10, result.TopDrifts.Count);

        var trackedCustomers = db.ChangeTracker.Entries<Customer>().Count();
        Assert.InRange(trackedCustomers, 0, 50);

        var remainingDrift = await db.Customers.AsNoTracking().CountAsync(x => x.CurrentBalance != 0m);
        Assert.Equal(0, remainingDrift);
    }

    private static ConGNoDbContext CreateDbContext(string testName)
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase(databaseName: $"customer-balance-reconcile-{testName}")
            .Options;
        return new ConGNoDbContext(options);
    }

    private static async Task SeedAsync(ConGNoDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var sellerCode = "SELLER-01";

        db.Sellers.Add(new Seller
        {
            SellerTaxCode = sellerCode,
            Name = "Seller 01",
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        });

        db.Customers.AddRange(
            new Customer
            {
                TaxCode = "CUST-001",
                Name = "Khach 001",
                Status = "ACTIVE",
                CurrentBalance = 999m,
                PaymentTermsDays = 30,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            },
            new Customer
            {
                TaxCode = "CUST-002",
                Name = "Khach 002",
                Status = "ACTIVE",
                CurrentBalance = 30m,
                PaymentTermsDays = 30,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            });

        db.Invoices.AddRange(
            new Invoice
            {
                Id = Guid.NewGuid(),
                SellerTaxCode = sellerCode,
                CustomerTaxCode = "CUST-001",
                InvoiceNo = "INV-001",
                IssueDate = DateOnly.FromDateTime(now.UtcDateTime),
                RevenueExclVat = 90m,
                VatAmount = 10m,
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
                SellerTaxCode = sellerCode,
                CustomerTaxCode = "CUST-001",
                InvoiceNo = "INV-VOID",
                IssueDate = DateOnly.FromDateTime(now.UtcDateTime),
                RevenueExclVat = 180m,
                VatAmount = 20m,
                TotalAmount = 200m,
                OutstandingAmount = 200m,
                InvoiceType = "NORMAL",
                Status = "VOID",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            },
            new Invoice
            {
                Id = Guid.NewGuid(),
                SellerTaxCode = sellerCode,
                CustomerTaxCode = "CUST-002",
                InvoiceNo = "INV-002",
                IssueDate = DateOnly.FromDateTime(now.UtcDateTime),
                RevenueExclVat = 45m,
                VatAmount = 5m,
                TotalAmount = 50m,
                OutstandingAmount = 50m,
                InvoiceType = "NORMAL",
                Status = "APPROVED",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            });

        db.Advances.AddRange(
            new Advance
            {
                Id = Guid.NewGuid(),
                SellerTaxCode = sellerCode,
                CustomerTaxCode = "CUST-001",
                AdvanceDate = DateOnly.FromDateTime(now.UtcDateTime),
                Amount = 40m,
                OutstandingAmount = 40m,
                Status = "APPROVED",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            },
            new Advance
            {
                Id = Guid.NewGuid(),
                SellerTaxCode = sellerCode,
                CustomerTaxCode = "CUST-001",
                AdvanceDate = DateOnly.FromDateTime(now.UtcDateTime),
                Amount = 300m,
                OutstandingAmount = 300m,
                Status = "DRAFT",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            });

        db.Receipts.AddRange(
            new Receipt
            {
                Id = Guid.NewGuid(),
                SellerTaxCode = sellerCode,
                CustomerTaxCode = "CUST-001",
                ReceiptDate = DateOnly.FromDateTime(now.UtcDateTime),
                Amount = 30m,
                Method = "BANK",
                AllocationMode = "FIFO",
                Status = "APPROVED",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            },
            new Receipt
            {
                Id = Guid.NewGuid(),
                SellerTaxCode = sellerCode,
                CustomerTaxCode = "CUST-001",
                ReceiptDate = DateOnly.FromDateTime(now.UtcDateTime),
                Amount = 500m,
                Method = "BANK",
                AllocationMode = "FIFO",
                Status = "VOID",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            },
            new Receipt
            {
                Id = Guid.NewGuid(),
                SellerTaxCode = sellerCode,
                CustomerTaxCode = "CUST-002",
                ReceiptDate = DateOnly.FromDateTime(now.UtcDateTime),
                Amount = 20m,
                Method = "BANK",
                AllocationMode = "FIFO",
                Status = "APPROVED",
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            });

        await db.SaveChangesAsync();
    }

    private static async Task SeedLargeCustomersAsync(ConGNoDbContext db, int customerCount)
    {
        var now = DateTimeOffset.UtcNow;
        var customers = Enumerable.Range(1, customerCount)
            .Select(i => new Customer
            {
                TaxCode = $"CUST-{i:D5}",
                Name = $"Khach {i:D5}",
                Status = "ACTIVE",
                CurrentBalance = 10m,
                PaymentTermsDays = 30,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0
            });

        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();
    }
}
