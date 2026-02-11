namespace CongNoGolden.Application.Imports;

public interface IImportPreviewService
{
    Task<ImportPreviewResult> PreviewAsync(Guid batchId, string? status, int page, int pageSize, CancellationToken ct);
}
