using System.Text.Json.Serialization;

namespace CongNoGolden.Application.ReceiptHeldCredits;

public sealed record ReceiptHeldCreditApplyRequest(
    Guid InvoiceId,
    [property: JsonPropertyName("use_general_credit_top_up")] bool UseGeneralCreditTopUp = false,
    [property: JsonPropertyName("version")] int? Version = null);
