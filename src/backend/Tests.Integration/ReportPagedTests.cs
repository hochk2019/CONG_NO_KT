using System.Collections;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongNoGolden.Tests.Integration;

[Collection("Database")]
public sealed class ReportPagedTests
{
    private readonly TestDatabaseFixture _fixture;

    public ReportPagedTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SummaryPaged_Sorts_By_CurrentBalance()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        await SeedCustomerAsync(db, "CUST-A", "Khach A", 300m);
        await SeedCustomerAsync(db, "CUST-B", "Khach B", 100m);
        await SeedCustomerAsync(db, "CUST-C", "Khach C", 200m);

        DapperTypeHandlers.Register();
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var service = new ReportService(connectionFactory);

        var requestType = RequireType("CongNoGolden.Application.Reports.ReportSummaryPagedRequest, CongNoGolden.Application");
        var request = Activator.CreateInstance(
            requestType,
            null,
            null,
            "customer",
            null,
            null,
            null,
            1,
            2,
            "currentBalance",
            "desc");

        var result = await InvokeAsync(service, "GetSummaryPagedAsync", request, CancellationToken.None);
        var total = GetProperty<int>(result, "Total");
        var items = GetItems(result);

        Assert.Equal(3, total);
        Assert.Equal(2, items.Count);
        Assert.Equal(300m, GetProperty<decimal>(items[0], "CurrentBalance"));
        Assert.Equal(200m, GetProperty<decimal>(items[1], "CurrentBalance"));
    }

    [Fact]
    public async Task AgingPaged_Sorts_By_Overdue_Descending()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var seller = await SeedSellerAsync(db, "SELLER01", "Seller 01");
        var customerA = await SeedCustomerAsync(db, "CUST-01", "Khach 01", 0m);
        var customerB = await SeedCustomerAsync(db, "CUST-02", "Khach 02", 0m);

        var asOf = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        await SeedInvoiceAsync(db, seller.SellerTaxCode, customerA.TaxCode, "INV-A", asOf.AddDays(-10), 100m);
        await SeedInvoiceAsync(db, seller.SellerTaxCode, customerB.TaxCode, "INV-B", asOf.AddDays(-40), 200m);

        DapperTypeHandlers.Register();
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var service = new ReportService(connectionFactory);

        var requestType = RequireType("CongNoGolden.Application.Reports.ReportAgingPagedRequest, CongNoGolden.Application");
        var request = Activator.CreateInstance(
            requestType,
            asOf,
            seller.SellerTaxCode,
            null,
            null,
            1,
            10,
            "overdue",
            "desc");

        var result = await InvokeAsync(service, "GetAgingPagedAsync", request, CancellationToken.None);
        var items = GetItems(result);

        Assert.Equal(2, items.Count);
        Assert.Equal("CUST-02", GetProperty<string>(items[0], "CustomerTaxCode"));
        Assert.Equal(200m, GetProperty<decimal>(items[0], "Overdue"));
    }

    [Fact]
    public async Task StatementPaged_Calculates_Running_Balance_Across_Pages()
    {
        await using var db = _fixture.CreateContext();
        await ResetAsync(db);

        var seller = await SeedSellerAsync(db, "SELLER02", "Seller 02");
        var customer = await SeedCustomerAsync(db, "CUST-ST", "Khach Statement", 0m);

        var from = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-10));
        var to = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        await SeedInvoiceAsync(db, seller.SellerTaxCode, customer.TaxCode, "INV-1", from.AddDays(1), 100m);
        await SeedInvoiceAsync(db, seller.SellerTaxCode, customer.TaxCode, "INV-2", from.AddDays(2), 50m);

        DapperTypeHandlers.Register();
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var service = new ReportService(connectionFactory);

        var requestType = RequireType("CongNoGolden.Application.Reports.ReportStatementPagedRequest, CongNoGolden.Application");
        var requestPage1 = Activator.CreateInstance(
            requestType,
            from,
            to,
            seller.SellerTaxCode,
            customer.TaxCode,
            1,
            1);
        var requestPage2 = Activator.CreateInstance(
            requestType,
            from,
            to,
            seller.SellerTaxCode,
            customer.TaxCode,
            2,
            1);

        var resultPage1 = await InvokeAsync(service, "GetStatementPagedAsync", requestPage1, CancellationToken.None);
        var resultPage2 = await InvokeAsync(service, "GetStatementPagedAsync", requestPage2, CancellationToken.None);

        var linesPage1 = GetLines(resultPage1);
        var linesPage2 = GetLines(resultPage2);

        Assert.Single(linesPage1);
        Assert.Single(linesPage2);
        Assert.Equal(100m, GetProperty<decimal>(linesPage1[0], "RunningBalance"));
        Assert.Equal(150m, GetProperty<decimal>(linesPage2[0], "RunningBalance"));
    }

    private static Type RequireType(string typeName)
    {
        var type = Type.GetType(typeName);
        Assert.NotNull(type);
        return type!;
    }

    private static async Task<object> InvokeAsync(object target, string methodName, object? request, CancellationToken ct)
    {
        var method = target.GetType().GetMethod(methodName);
        Assert.NotNull(method);
        var task = (Task)method!.Invoke(target, new[] { request, ct })!;
        await task;
        var resultProperty = task.GetType().GetProperty("Result");
        Assert.NotNull(resultProperty);
        var result = resultProperty!.GetValue(task);
        Assert.NotNull(result);
        return result!;
    }

    private static List<object> GetItems(object result)
    {
        var itemsProperty = result.GetType().GetProperty("Items");
        Assert.NotNull(itemsProperty);
        var items = itemsProperty!.GetValue(result) as IEnumerable;
        Assert.NotNull(items);
        return items!.Cast<object>().ToList();
    }

    private static List<object> GetLines(object result)
    {
        var linesProperty = result.GetType().GetProperty("Lines");
        Assert.NotNull(linesProperty);
        var items = linesProperty!.GetValue(result) as IEnumerable;
        Assert.NotNull(items);
        return items!.Cast<object>().ToList();
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        var value = property!.GetValue(target);
        Assert.NotNull(value);
        return (T)value!;
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

    private static async Task<Customer> SeedCustomerAsync(
        ConGNoDbContext db,
        string taxCode,
        string name,
        decimal currentBalance)
    {
        var customer = new Customer
        {
            TaxCode = taxCode,
            Name = name,
            Status = "ACTIVE",
            PaymentTermsDays = 0,
            CurrentBalance = currentBalance,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    private static async Task<Seller> SeedSellerAsync(ConGNoDbContext db, string taxCode, string name)
    {
        var seller = new Seller
        {
            SellerTaxCode = taxCode,
            Name = name,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        };
        db.Sellers.Add(seller);
        await db.SaveChangesAsync();
        return seller;
    }

    private static async Task SeedInvoiceAsync(
        ConGNoDbContext db,
        string sellerTaxCode,
        string customerTaxCode,
        string invoiceNo,
        DateOnly issueDate,
        decimal amount)
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
            Status = "APPROVED",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 0
        });

        await db.SaveChangesAsync();
    }
}
