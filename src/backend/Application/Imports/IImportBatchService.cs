using CongNoGolden.Application.Common;

namespace CongNoGolden.Application.Imports;

public interface IImportBatchService
{
    Task<ImportBatchDto> CreateBatchAsync(CreateImportBatchRequest request, CancellationToken ct);
    Task<PagedResult<ImportBatchListItem>> ListAsync(ImportBatchListRequest request, CancellationToken ct);
}
