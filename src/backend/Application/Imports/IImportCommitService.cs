namespace CongNoGolden.Application.Imports;

public interface IImportCommitService
{
    Task<ImportCommitResult> CommitAsync(Guid batchId, ImportCommitRequest request, CancellationToken ct);
}
