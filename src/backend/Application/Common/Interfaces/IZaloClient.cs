namespace CongNoGolden.Application.Common.Interfaces;

public sealed record ZaloSendResult(bool Success, string? Error);

public interface IZaloClient
{
    Task<ZaloSendResult> SendAsync(string userId, string message, CancellationToken ct);
}
