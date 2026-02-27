using CongNoGolden.Api;
using CongNoGolden.Api.Security;
using CongNoGolden.Application.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", async (
            LoginRequest request,
            IAuthService authService,
            IOptions<JwtOptions> jwtOptions,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("AuthEndpoints");
                logger.LogInformation("Auth login attempt for {Username}", request.Username);
            try
            {
                var session = await authService.LoginAsync(request, BuildRequestContext(httpContext), ct);
                SetRefreshCookie(httpContext, jwtOptions.Value, session.RefreshToken, session.RefreshExpiresAt);
                logger.LogInformation("Auth login success for {Username}", request.Username);
                return Results.Ok(session.Access);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Auth login failed for {Username}", request.Username);
                return ApiErrors.Unauthorized(ex.Message);
            }
        })
        .WithName("AuthLogin")
        .WithTags("Auth")
        .RequireRateLimiting(AuthSecurityPolicy.LoginRateLimiterPolicy)
        .AllowAnonymous();

        app.MapPost("/auth/refresh", async (
            IAuthService authService,
            IOptions<JwtOptions> jwtOptions,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("AuthEndpoints");
            var cookieName = jwtOptions.Value.RefreshCookieName;
            if (!httpContext.Request.Cookies.TryGetValue(cookieName, out var refreshToken))
            {
                logger.LogWarning("Auth refresh missing cookie {CookieName}", cookieName);
                return ApiErrors.Unauthorized("Refresh token missing.");
            }

            try
            {
                var session = await authService.RefreshAsync(refreshToken, BuildRequestContext(httpContext), ct);
                SetRefreshCookie(httpContext, jwtOptions.Value, session.RefreshToken, session.RefreshExpiresAt);
                logger.LogInformation("Auth refresh success");
                return Results.Ok(session.Access);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Auth refresh failed");
                return ApiErrors.Unauthorized(ex.Message);
            }
        })
        .WithName("AuthRefresh")
        .WithTags("Auth")
        .RequireRateLimiting(AuthSecurityPolicy.RefreshRateLimiterPolicy)
        .AllowAnonymous();

        app.MapPost("/auth/logout", async (
            IAuthService authService,
            IOptions<JwtOptions> jwtOptions,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("AuthEndpoints");
            var cookieName = jwtOptions.Value.RefreshCookieName;
            if (httpContext.Request.Cookies.TryGetValue(cookieName, out var refreshToken))
            {
                await authService.RevokeAsync(refreshToken, ct);
            }

            ClearRefreshCookie(httpContext, jwtOptions.Value);
            logger.LogInformation("Auth logout");
            return Results.NoContent();
        })
        .WithName("AuthLogout")
        .WithTags("Auth")
        .AllowAnonymous();

        return app;
    }

    private static AuthRequestContext BuildRequestContext(HttpContext httpContext)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        return new AuthRequestContext(ip, userAgent);
    }

    private static void SetRefreshCookie(
        HttpContext httpContext,
        JwtOptions options,
        string refreshToken,
        DateTimeOffset expiresAt)
    {
        var sameSite = AuthSecurityPolicy.ResolveSameSiteMode(options.RefreshCookieSameSite);
        var cookiePath = AuthSecurityPolicy.ResolveCookiePath(options.RefreshCookiePath);

        httpContext.Response.Cookies.Append(
            options.RefreshCookieName,
            refreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = options.RefreshCookieSecure,
                SameSite = sameSite,
                Path = cookiePath,
                Expires = expiresAt.UtcDateTime
            });
    }

    private static void ClearRefreshCookie(HttpContext httpContext, JwtOptions options)
    {
        var sameSite = AuthSecurityPolicy.ResolveSameSiteMode(options.RefreshCookieSameSite);
        var cookiePath = AuthSecurityPolicy.ResolveCookiePath(options.RefreshCookiePath);

        httpContext.Response.Cookies.Delete(options.RefreshCookieName, new CookieOptions
        {
            Path = cookiePath,
            Secure = options.RefreshCookieSecure,
            SameSite = sameSite
        });
    }
}
