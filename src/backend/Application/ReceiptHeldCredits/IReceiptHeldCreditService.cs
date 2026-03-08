using CongNoGolden.Application.Common;

namespace CongNoGolden.Application.ReceiptHeldCredits;

public interface IReceiptHeldCreditService
{
    Task<PagedResult<ReceiptHeldCreditListItem>> ListByCustomerAsync(
        string customerTaxCode,
        ReceiptHeldCreditListRequest request,
        CancellationToken ct);

    Task<ReceiptHeldCreditApplyResult> ApplyToInvoiceAsync(
        Guid heldCreditId,
        ReceiptHeldCreditApplyRequest request,
        CancellationToken ct);

    Task<ReceiptHeldCreditReleaseResult> ReleaseToGeneralCreditAsync(
        Guid heldCreditId,
        ReceiptHeldCreditReleaseRequest request,
        CancellationToken ct);
}
