using System.Security.Cryptography;
using System.Text.Json;
using CongNoGolden.Api;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Api.Endpoints;

public static class ZaloEndpoints
{
    public static IEndpointRouteBuilder MapZaloEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/zalo/link/status", async (
            ConGNoDbContext db,
            ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            if (currentUser.UserId is null)
            {
                return Results.Unauthorized();
            }

            var user = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == currentUser.UserId)
                .Select(u => new { u.ZaloUserId, u.ZaloLinkedAt })
                .FirstOrDefaultAsync(ct);

            if (user is null)
            {
                return ApiErrors.NotFound("User not found.");
            }

            return Results.Ok(new ZaloLinkStatusResponse(
                !string.IsNullOrWhiteSpace(user.ZaloUserId),
                user.ZaloUserId,
                user.ZaloLinkedAt));
        })
        .WithName("ZaloLinkStatus")
        .WithTags("Zalo")
        .RequireAuthorization();

        app.MapPost("/zalo/link/request", async (
            ConGNoDbContext db,
            ICurrentUser currentUser,
            IOptions<ZaloOptions> options,
            CancellationToken ct) =>
        {
            if (currentUser.UserId is null)
            {
                return Results.Unauthorized();
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct);
            if (user is null)
            {
                return ApiErrors.NotFound("User not found.");
            }

            if (!string.IsNullOrWhiteSpace(user.ZaloUserId))
            {
                return ApiErrors.Conflict("User already linked to Zalo.");
            }

            var now = DateTimeOffset.UtcNow;
            var expireMinutes = options.Value.LinkCodeMinutes <= 0 ? 15 : options.Value.LinkCodeMinutes;
            var expiresAt = now.AddMinutes(expireMinutes);

            var existingTokens = await db.ZaloLinkTokens
                .Where(t => t.UserId == user.Id && t.ConsumedAt == null && t.ExpiresAt > now)
                .ToListAsync(ct);

            foreach (var token in existingTokens)
            {
                token.ConsumedAt = now;
            }

            var code = await GenerateUniqueCodeAsync(db, ct);
            db.ZaloLinkTokens.Add(new ZaloLinkToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Code = code,
                ExpiresAt = expiresAt,
                CreatedAt = now
            });

            await db.SaveChangesAsync(ct);

            return Results.Ok(new ZaloLinkCodeResponse(code, expiresAt));
        })
        .WithName("ZaloLinkRequest")
        .WithTags("Zalo")
        .RequireAuthorization();

        app.MapPost("/webhooks/zalo", async (
            HttpContext context,
            ConGNoDbContext db,
            IAuditService auditService,
            IOptions<ZaloOptions> options,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("ZaloWebhook");
            if (!IsWebhookAuthorized(context, options.Value))
            {
                return Results.Unauthorized();
            }

            JsonDocument document;
            try
            {
                document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: ct);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { status = "invalid_payload" });
            }

            using (document)
            {
                var root = document.RootElement;

                if (!ZaloWebhookParser.TryExtractMessageText(root, out var messageText))
                {
                    return Results.Ok(new { status = "ignored" });
                }

                if (!ZaloWebhookParser.TryExtractUserId(root, out var senderId))
                {
                    logger.LogWarning("Zalo webhook missing sender id.");
                    return Results.Ok(new { status = "ignored" });
                }

                if (!string.IsNullOrWhiteSpace(options.Value.OaId)
                    && ZaloWebhookParser.TryExtractRecipientOaId(root, out var oaId)
                    && !string.Equals(options.Value.OaId, oaId, StringComparison.Ordinal))
                {
                    logger.LogWarning("Zalo webhook OA id mismatch.");
                    return Results.Forbid();
                }

                if (!ZaloWebhookParser.TryExtractLinkCode(messageText, out var code))
                {
                    return Results.Ok(new { status = "ignored" });
                }

                var now = DateTimeOffset.UtcNow;
                var token = await db.ZaloLinkTokens
                    .FirstOrDefaultAsync(t => t.Code == code, ct);

                if (token is null || token.ConsumedAt != null || token.ExpiresAt <= now)
                {
                    logger.LogWarning("Zalo link token invalid or expired.");
                    return Results.Ok(new { status = "invalid" });
                }

                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
                if (user is null)
                {
                    token.ConsumedAt = now;
                    await db.SaveChangesAsync(ct);
                    return Results.Ok(new { status = "invalid" });
                }

                var existingUser = await db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.ZaloUserId == senderId && u.Id != user.Id, ct);
                if (existingUser is not null)
                {
                    token.ConsumedAt = now;
                    await db.SaveChangesAsync(ct);
                    logger.LogWarning("Zalo user id already linked to another user.");
                    return Results.Ok(new { status = "conflict" });
                }

                var before = new { user.ZaloUserId, user.ZaloLinkedAt };
                user.ZaloUserId = senderId;
                user.ZaloLinkedAt = now;
                token.ConsumedAt = now;

                await db.SaveChangesAsync(ct);

                await auditService.LogAsync(
                    "ZALO_LINK",
                    "User",
                    user.Id.ToString(),
                    before,
                    new { user.ZaloUserId, user.ZaloLinkedAt },
                    ct);

                return Results.Ok(new { status = "linked" });
            }
        })
        .WithName("ZaloWebhook")
        .WithTags("Zalo")
        .AllowAnonymous();

        app.MapGet("/webhooks/zalo", (HttpContext context, IOptions<ZaloOptions> options) =>
        {
            if (!IsWebhookAuthorized(context, options.Value))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new { status = "ok" });
        })
        .WithName("ZaloWebhookPing")
        .WithTags("Zalo")
        .AllowAnonymous();

        return app;
    }

    private static bool IsWebhookAuthorized(HttpContext context, ZaloOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.WebhookToken))
        {
            return true;
        }

        var provided = context.Request.Query["token"].ToString();
        if (string.IsNullOrWhiteSpace(provided))
        {
            provided = context.Request.Headers["X-Zalo-Token"].ToString();
        }

        return string.Equals(options.WebhookToken, provided, StringComparison.Ordinal);
    }

    private static async Task<string> GenerateUniqueCodeAsync(ConGNoDbContext db, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var exists = await db.ZaloLinkTokens.AnyAsync(t => t.Code == code, ct);
            if (!exists)
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate link code.");
    }
}

public sealed record ZaloLinkStatusResponse(bool Linked, string? ZaloUserId, DateTimeOffset? LinkedAt);

public sealed record ZaloLinkCodeResponse(string Code, DateTimeOffset ExpiresAt);
