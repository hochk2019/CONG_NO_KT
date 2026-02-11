using System.Text.Json;

namespace CongNoGolden.Application.Imports;

public sealed record ImportPreviewRow(
    int RowNo,
    string ValidationStatus,
    JsonElement RawData,
    JsonElement ValidationMessages,
    string? DedupKey,
    string? ActionSuggestion
);
