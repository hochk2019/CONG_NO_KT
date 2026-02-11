namespace CongNoGolden.Application.Common.Interfaces;

public interface IAuditService
{
    Task LogAsync(string action, string entityType, string entityId, object? before, object? after, CancellationToken ct);
}
