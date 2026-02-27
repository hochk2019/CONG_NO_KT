using CongNoGolden.Api;
using CongNoGolden.Application.Search;

namespace CongNoGolden.Api.Endpoints;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/search/global", async (
            string? q,
            int? top,
            IGlobalSearchService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return ApiErrors.InvalidRequest("Query is required.");
            }

            var query = q.Trim();
            if (query.Length < 2)
            {
                return ApiErrors.InvalidRequest("Query must be at least 2 characters.");
            }

            var take = NormalizeTop(top);
            if (take is null)
            {
                return ApiErrors.InvalidRequest("Invalid top parameter.");
            }

            var result = await service.SearchAsync(query, take.Value, ct);
            return Results.Ok(result);
        })
        .WithName("SearchGlobal")
        .WithTags("Search")
        .RequireAuthorization("CustomerView");

        return app;
    }

    private static int? NormalizeTop(int? top)
    {
        var value = top.GetValueOrDefault(6);
        if (value <= 0 || value > 20)
        {
            return null;
        }

        return value;
    }
}
