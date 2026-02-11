namespace CongNoGolden.Application.Imports;

public interface IImportRollbackService
{
    Task<ImportRollbackResult> RollbackAsync(Guid batchId, ImportRollbackRequest request, CancellationToken ct);
}
