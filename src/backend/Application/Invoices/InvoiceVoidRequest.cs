using System.Text.Json.Serialization;

namespace CongNoGolden.Application.Invoices;

public sealed record InvoiceVoidRequest(
    string? Reason,
    Guid? ReplacementInvoiceId,
    [property: JsonPropertyName("force")] bool Force = false,
    [property: JsonPropertyName("version")] int? Version = null
);
