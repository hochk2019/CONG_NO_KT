using System.Data.Common;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public sealed class ReceiptAutomationServiceTests
{
    private const int MaxSelectsForBatchLoad = 7;
    private readonly TestDatabaseFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ReceiptAutomationServiceTests(TestDatabaseFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task RunAsync_BatchesOpenItemQueries_PerSellerCustomer()
    {
        await using var seedDb = _fixture.CreateContext();
        await ResetAsync(seedDb);

        var (seller, customer) = await SeedMasterAsync(seedDb);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var now = DateTimeOffset.UtcNow;

        await SeedInvoiceAsync(seedDb, seller.SellerTaxCode, customer.TaxCode, "INV-01", today.AddDays(-3), 1_000, "APPROVED");
        await SeedInvoiceAsync(seedDb, seller.SellerTaxCode, customer.TaxCode, "INV-02", today.AddDays(-2), 1_500, "APPROVED");
        await SeedAdvanceAsync(seedDb, seller.SellerTaxCode, customer.TaxCode, today.AddDays(-1), 500, "APPROVED");

        await SeedReceiptAsync(seedDb, seller.SellerTaxCode, customer.TaxCode, now, "R-01");
        await SeedReceiptAsync(seedDb, seller.SellerTaxCode, customer.TaxCode, now, "R-02");
        await SeedReceiptAsync(seedDb, seller.SellerTaxCode, customer.TaxCode, now, "R-03");

        var counter = new SelectCommandCounter();
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(counter)
            .Options;

        await using var db = new ConGNoDbContext(options);
        var service = new ReceiptAutomationService(db, NullLogger<ReceiptAutomationService>.Instance);
        counter.Reset();
        await db.Customers.AsNoTracking().ToListAsync();
        Assert.True(counter.SelectCount > 0, "Expected SELECT interceptor to capture queries.");
        counter.Reset();
        await service.RunAsync(CancellationToken.None);

        var selectCount = counter.SelectCount;
        _output.WriteLine("SELECT count during RunAsync: {0}", selectCount);
        Assert.True(selectCount <= MaxSelectsForBatchLoad,
            $"Expected <= {MaxSelectsForBatchLoad} SELECTs, got {selectCount}.");

        await using var verifyDb = _fixture.CreateContext();
        var receipts = await verifyDb.Receipts
            .AsNoTracking()
            .Where(r => r.CustomerTaxCode == customer.TaxCode && r.SellerTaxCode == seller.SellerTaxCode)
            .ToListAsync();

        Assert.All(receipts, receipt =>
        {
            Assert.Equal("SUGGESTED", receipt.AllocationStatus);
            Assert.False(string.IsNullOrWhiteSpace(receipt.AllocationTargets));
        });
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
            "congno.sellers " +
            "RESTART IDENTITY CASCADE;");
    }

    private static async Task<(Seller seller, Customer customer)> SeedMasterAsync(ConGNoDbContext db)
    {
        var seller = new Seller
        {
            SellerTaxCode = "SELLER01",
            Name = "Seller 01",
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };
        var customer = new Customer
        {
            TaxCode = "CUST01",
            Name = "Customer 01",
            Status = "ACTIVE",
            PaymentTermsDays = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };

        db.Sellers.Add(seller);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return (seller, customer);
    }

    private static async Task SeedInvoiceAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        string invoiceNo,
        DateOnly issueDate,
        decimal amount,
        string status)
    {
        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            InvoiceNo = invoiceNo,
            IssueDate = issueDate,
            RevenueExclVat = amount,
            VatAmount = 0,
            TotalAmount = amount,
            OutstandingAmount = amount,
            InvoiceType = "SALE",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedAdvanceAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        DateOnly advanceDate,
        decimal amount,
        string status)
    {
        db.Advances.Add(new Advance
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            AdvanceNo = "ADV-01",
            AdvanceDate = advanceDate,
            Amount = amount,
            OutstandingAmount = amount,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedReceiptAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        DateTimeOffset now,
        string receiptNo)
    {
        db.Receipts.Add(new Receipt
        {
            Id = Guid.NewGuid(),
            SellerTaxCode = sellerTaxCode,
            CustomerTaxCode = customerTaxCode,
            ReceiptNo = receiptNo,
            ReceiptDate = DateOnly.FromDateTime(now.Date),
            Amount = 500_000,
            Method = "BANK",
            AllocationMode = "MANUAL",
            AllocationStatus = "UNALLOCATED",
            AllocationPriority = "ISSUE_DATE",
            Status = "DRAFT",
            UnallocatedAmount = 500_000,
            ReminderDisabledAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        });

        await db.SaveChangesAsync();
    }

    private sealed class SelectCommandCounter : DbCommandInterceptor
    {
        private int _selectCount;

        public int SelectCount => _selectCount;

        public void Reset()
        {
            _selectCount = 0;
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            CountIfSelect(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            CountIfSelect(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result)
        {
            CountIfSelect(command.CommandText);
            return base.ScalarExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            CountIfSelect(command.CommandText);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void CountIfSelect(string? commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText))
            {
                return;
            }

            var trimmed = commandText.TrimStart();
            if (trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                _selectCount += 1;
            }
        }
    }
}
