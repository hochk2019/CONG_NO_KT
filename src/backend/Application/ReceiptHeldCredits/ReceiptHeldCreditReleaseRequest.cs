using System.Text.Json.Serialization;

namespace CongNoGolden.Application.ReceiptHeldCredits;

public sealed record ReceiptHeldCreditReleaseRequest(
    [property: JsonPropertyName("version")] int? Version = null);
