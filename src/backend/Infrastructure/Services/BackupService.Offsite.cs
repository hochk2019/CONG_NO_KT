using CongNoGolden.Application.Backups;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.Extensions.Logging;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class BackupService
{
    private async Task<BackupSettingsDto> MapSettingsAsync(BackupSettings settings, CancellationToken ct)
    {
        var offsiteConnection = await GetOffsiteConnectionStatusAsync(ct);
        return MapSettings(settings, offsiteConnection);
    }

    private async Task<BackupOffsiteConnectionStatus?> GetOffsiteConnectionStatusAsync(CancellationToken ct)
    {
        try
        {
            return await _backupOffsiteService.GetConnectionStatusAsync(ct);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task TryQueueOffsiteUploadAsync(BackupSettings settings, Guid backupJobId, CancellationToken ct)
    {
        if (!await ShouldQueueOffsiteUploadAsync(settings, ct))
        {
            return;
        }

        try
        {
            await _backupOffsiteService.EnqueueUploadForBackupJobAsync(backupJobId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enqueue offsite upload for backup job {BackupJobId}", backupJobId);
        }
    }

    private async Task<bool> ShouldQueueOffsiteUploadAsync(BackupSettings settings, CancellationToken ct)
    {
        if (!settings.OffsiteEnabled ||
            !settings.UploadAfterBackup ||
            string.IsNullOrWhiteSpace(settings.OffsiteProvider))
        {
            return false;
        }

        var offsiteConnection = await GetOffsiteConnectionStatusAsync(ct);
        return offsiteConnection?.IsConnected == true;
    }
}
