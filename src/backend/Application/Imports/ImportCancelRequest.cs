using System.Text.Json.Serialization;

namespace CongNoGolden.Application.Imports;

public sealed record ImportCancelRequest(
    [property: JsonPropertyName("reason")] string? Reason = null
);
