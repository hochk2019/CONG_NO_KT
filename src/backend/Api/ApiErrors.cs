using CongNoGolden.Application.Common;
using Microsoft.AspNetCore.Http;

namespace CongNoGolden.Api;

public static class ApiErrors
{
    public static IResult InvalidRequest(string detail, string code = "INVALID_REQUEST")
        => Problem(StatusCodes.Status400BadRequest, "Bad Request", detail, code);

    public static IResult Unauthorized(string detail, string code = "UNAUTHORIZED")
        => Problem(StatusCodes.Status401Unauthorized, "Unauthorized", detail, code);

    public static IResult Forbidden(string detail, string code = "FORBIDDEN")
        => Problem(StatusCodes.Status403Forbidden, "Forbidden", detail, code);

    public static IResult NotFound(string detail, string code = "NOT_FOUND")
        => Problem(StatusCodes.Status404NotFound, "Not Found", detail, code);

    public static IResult Conflict(string detail, string code = "CONFLICT")
        => Problem(StatusCodes.Status409Conflict, "Conflict", detail, code);

    public static IResult Concurrency(string detail, string code = "CONCURRENCY_CONFLICT")
        => Problem(StatusCodes.Status409Conflict, "Concurrency Conflict", detail, code);

    public static IResult FromException(Exception ex)
    {
        return ex switch
        {
            ConcurrencyException => Concurrency(ex.Message),
            UnauthorizedAccessException => Forbidden(ex.Message),
            InvalidOperationException => InvalidRequest(ex.Message, "INVALID_OPERATION"),
            ArgumentException => InvalidRequest(ex.Message),
            KeyNotFoundException => NotFound(ex.Message),
            _ => Problem(StatusCodes.Status500InternalServerError, "Server Error", "Unexpected error.", "SERVER_ERROR")
        };
    }

    private static IResult Problem(int status, string title, string detail, string code)
    {
        return Results.Problem(
            title: title,
            detail: detail,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = code });
    }
}
