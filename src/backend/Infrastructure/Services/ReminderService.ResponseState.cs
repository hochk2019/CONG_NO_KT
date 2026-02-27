using CongNoGolden.Application.Reminders;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReminderService
{
    private static readonly HashSet<string> AllowedResponseStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "NO_RESPONSE",
        "ACKNOWLEDGED",
        "DISPUTED",
        "RESOLVED"
    };

    public async Task<ReminderResponseStateDto?> GetResponseStateAsync(
        string customerTaxCode,
        string channel,
        CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var normalizedCustomerTaxCode = NormalizeCustomerTaxCode(customerTaxCode);
        var normalizedChannel = NormalizeChannel(channel);
        if (normalizedChannel is null)
        {
            throw new InvalidOperationException("Channel is required.");
        }

        var state = await _db.ReminderResponseStates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.CustomerTaxCode == normalizedCustomerTaxCode && x.Channel == normalizedChannel,
                ct);

        return state is null ? null : MapResponseState(state);
    }

    public async Task<ReminderResponseStateDto> UpsertResponseStateAsync(
        ReminderResponseStateUpsertRequest request,
        CancellationToken ct)
    {
        _currentUser.EnsureUser();

        var normalizedCustomerTaxCode = NormalizeCustomerTaxCode(request.CustomerTaxCode);
        var normalizedChannel = NormalizeChannel(request.Channel);
        if (normalizedChannel is null)
        {
            throw new InvalidOperationException("Channel is required.");
        }

        var normalizedStatus = NormalizeResponseStatus(request.ResponseStatus);
        var now = DateTimeOffset.UtcNow;

        var state = await _db.ReminderResponseStates.FirstOrDefaultAsync(
            x => x.CustomerTaxCode == normalizedCustomerTaxCode && x.Channel == normalizedChannel,
            ct);

        var before = state is null
            ? null
            : new
            {
                state.ResponseStatus,
                state.LatestResponseAt,
                state.EscalationLocked,
                state.AttemptCount,
                state.CurrentEscalationLevel,
                state.LastSentAt
            };

        if (state is null)
        {
            state = new ReminderResponseState
            {
                Id = Guid.NewGuid(),
                CustomerTaxCode = normalizedCustomerTaxCode,
                Channel = normalizedChannel,
                CreatedAt = now
            };
            _db.ReminderResponseStates.Add(state);
        }

        state.ResponseStatus = normalizedStatus;
        state.LatestResponseAt = request.ResponseAt ?? now;
        state.EscalationLocked = request.EscalationLocked ?? DefaultEscalationLock(normalizedStatus);
        state.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        var after = new
        {
            state.ResponseStatus,
            state.LatestResponseAt,
            state.EscalationLocked,
            state.AttemptCount,
            state.CurrentEscalationLevel,
            state.LastSentAt
        };

        await _auditService.LogAsync(
            "REMINDER_RESPONSE_STATE_UPDATE",
            "ReminderResponseState",
            state.Id.ToString(),
            before,
            after,
            ct);

        return MapResponseState(state);
    }

    private static ReminderResponseStateDto MapResponseState(ReminderResponseState state)
    {
        return new ReminderResponseStateDto(
            state.CustomerTaxCode,
            state.Channel,
            state.ResponseStatus,
            state.LatestResponseAt,
            state.EscalationLocked,
            state.AttemptCount,
            state.CurrentEscalationLevel,
            state.LastSentAt,
            state.UpdatedAt);
    }

    private static string NormalizeCustomerTaxCode(string customerTaxCode)
    {
        if (string.IsNullOrWhiteSpace(customerTaxCode))
        {
            throw new InvalidOperationException("Customer tax code is required.");
        }

        var normalized = customerTaxCode.Trim().ToUpperInvariant();
        return normalized;
    }

    private static string NormalizeResponseStatus(string responseStatus)
    {
        if (string.IsNullOrWhiteSpace(responseStatus))
        {
            throw new InvalidOperationException("Response status is required.");
        }

        var normalized = responseStatus.Trim().ToUpperInvariant();
        if (normalized == "PENDING")
        {
            normalized = "NO_RESPONSE";
        }

        if (!AllowedResponseStatuses.Contains(normalized))
        {
            throw new InvalidOperationException("Unsupported response status.");
        }

        return normalized;
    }

    private static bool DefaultEscalationLock(string responseStatus)
    {
        return responseStatus is "ACKNOWLEDGED" or "RESOLVED";
    }
}
