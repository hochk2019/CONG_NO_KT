namespace CongNoGolden.Application.Imports;

public interface IImportStagingService
{
    Task<ImportStagingResult> StageAsync(Guid batchId, string type, Stream fileStream, CancellationToken ct);
}
