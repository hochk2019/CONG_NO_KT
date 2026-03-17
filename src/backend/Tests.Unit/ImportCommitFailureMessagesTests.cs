using CongNoGolden.Infrastructure.Services;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class ImportCommitFailureMessagesTests
{
    [Theory]
    [InlineData("INVOICE", "invoices_customer_tax_code_fkey", "hóa đơn")]
    [InlineData("ADVANCE", "advances_customer_tax_code_fkey", "khoản trả hộ")]
    [InlineData("RECEIPT", "receipts_customer_tax_code_fkey", "phiếu thu")]
    public void TryBuildConstraintMessage_ReturnsGuidance_ForMissingCustomerForeignKey(
        string batchType,
        string constraintName,
        string expectedDocumentLabel)
    {
        var message = ImportCommitFailureMessages.TryBuildConstraintMessage(constraintName, batchType);

        Assert.NotNull(message);
        Assert.Contains(expectedDocumentLabel, message!);
        Assert.Contains("MST khách hàng", message!);
        Assert.Contains("tạo/cập nhật khách hàng", message!);
    }

    [Fact]
    public void TryBuildConstraintMessage_ReturnsNull_ForUnknownConstraint()
    {
        var message = ImportCommitFailureMessages.TryBuildConstraintMessage("unknown_constraint", "INVOICE");

        Assert.Null(message);
    }
}
