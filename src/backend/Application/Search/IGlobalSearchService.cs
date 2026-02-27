namespace CongNoGolden.Application.Search;

public interface IGlobalSearchService
{
    Task<GlobalSearchResultDto> SearchAsync(string query, int top, CancellationToken ct);
}
