using System.Text.Json;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public static class ImportCommitCustomers
{
    public static async Task<Customer?> EnsureCustomer(
        ConGNoDbContext db,
        JsonElement raw,
        Dictionary<string, Customer> cache,
        CancellationToken ct)
    {
        var taxCode = ImportCommitJson.GetString(raw, "customer_tax_code");
        if (string.IsNullOrWhiteSpace(taxCode))
        {
            return null;
        }

        if (cache.TryGetValue(taxCode, out var existing))
        {
            return existing;
        }

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.TaxCode == taxCode, ct);
        if (customer is null)
        {
            customer = new Customer
            {
                TaxCode = taxCode,
                Name = ImportCommitJson.GetString(raw, "customer_name") ?? taxCode,
                Status = "ACTIVE",
                CurrentBalance = 0,
                PaymentTermsDays = 30,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Version = 0
            };
            db.Customers.Add(customer);
        }

        cache[taxCode] = customer;
        return customer;
    }
}
