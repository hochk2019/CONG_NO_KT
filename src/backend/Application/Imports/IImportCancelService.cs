namespace CongNoGolden.Application.Imports;

public interface IImportCancelService
{
    Task<ImportCancelResult> CancelAsync(Guid batchId, ImportCancelRequest request, CancellationToken ct);
}
