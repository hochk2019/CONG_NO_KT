using System.Reflection;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Infrastructure.Services;
using Xunit;

namespace Tests.Unit;

public class ReceiptAutomationServiceTests
{
    [Fact]
    public void BuildOrderedTargets_UsesIssueDateAndInvoicePriority()
    {
        var invoiceId = Guid.NewGuid();
        var advanceId = Guid.NewGuid();
        var laterInvoiceId = Guid.NewGuid();

        var items = new List<ReceiptOpenItemDto>
        {
            new("INVOICE", invoiceId, "HD-001", new DateOnly(2025, 1, 10), new DateOnly(2025, 2, 10), 100, "S1", "C1"),
            new("ADVANCE", advanceId, "TH-001", new DateOnly(2025, 1, 10), new DateOnly(2025, 2, 10), 200, "S1", "C1"),
            new("INVOICE", laterInvoiceId, "HD-002", new DateOnly(2025, 1, 15), new DateOnly(2025, 2, 15), 300, "S1", "C1"),
        };

        var result = InvokeBuildOrderedTargets(items, "ISSUE_DATE");

        Assert.Equal(invoiceId, result[0].Id);
        Assert.Equal(advanceId, result[1].Id);
        Assert.Equal(laterInvoiceId, result[2].Id);
    }

    [Fact]
    public void BuildOrderedTargets_UsesDueDateWhenConfigured()
    {
        var invoiceId = Guid.NewGuid();
        var advanceId = Guid.NewGuid();

        var items = new List<ReceiptOpenItemDto>
        {
            new("INVOICE", invoiceId, "HD-001", new DateOnly(2025, 1, 15), new DateOnly(2025, 3, 1), 100, "S1", "C1"),
            new("ADVANCE", advanceId, "TH-001", new DateOnly(2025, 1, 1), new DateOnly(2025, 2, 1), 200, "S1", "C1"),
        };

        var result = InvokeBuildOrderedTargets(items, "DUE_DATE");

        Assert.Equal(advanceId, result[0].Id);
        Assert.Equal(invoiceId, result[1].Id);
    }

    private static List<ReceiptTargetRef> InvokeBuildOrderedTargets(
        IReadOnlyList<ReceiptOpenItemDto> items,
        string priority)
    {
        var method = typeof(ReceiptAutomationService).GetMethod(
            "BuildOrderedTargets",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method is null)
        {
            throw new InvalidOperationException("BuildOrderedTargets method not found.");
        }

        var result = method.Invoke(null, new object?[] { items, priority });

        return result as List<ReceiptTargetRef> ?? throw new InvalidOperationException("Invalid ordering result.");
    }
}
