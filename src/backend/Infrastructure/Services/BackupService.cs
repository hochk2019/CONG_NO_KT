using System.Diagnostics;
using System.Text.Json;
using CongNoGolden.Application.Backups;
using CongNoGolden.Application.Common;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class BackupService : IBackupService
{
    private const string WindowsDefaultBackupPath = @"C:\apps\congno\backup\dumps";
    private const string LinuxDefaultBackupPath = "/var/lib/congno/backups/dumps";
    private const string WindowsDefaultPgBinPath = @"C:\Program Files\PostgreSQL\16\bin";
    private const string LinuxDefaultPgBinPath = "/usr/bin";

    private static readonly TimeSpan DefaultTokenTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan UploadTtl = TimeSpan.FromHours(24);
    private const int LogLimit = 20000;
    private const string BackupLockKey = "backup_job";
    private const string RestoreLockKey = "backup_restore";
    private const string DefaultSchema = "congno";

    private readonly ConGNoDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IMaintenanceState _maintenanceState;
    private readonly BackupQueue _queue;
    private readonly BackupProcessRunner _processRunner;
    private readonly ILogger<BackupService> _logger;
    private readonly IConfiguration _configuration;

    public BackupService(
        ConGNoDbContext db,
        ICurrentUser currentUser,
        IMaintenanceState maintenanceState,
        BackupQueue queue,
        BackupProcessRunner processRunner,
        ILogger<BackupService> logger,
        IConfiguration configuration)
    {
        _db = db;
        _currentUser = currentUser;
        _maintenanceState = maintenanceState;
        _queue = queue;
        _processRunner = processRunner;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<BackupSettingsDto> GetSettingsAsync(CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(ct);
        return MapSettings(settings);
    }

    public async Task<BackupSettingsDto> UpdateSettingsAsync(
        BackupSettingsUpdateRequest request,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(ct);

        var backupPath = NormalizeBackupPathForRuntime(request.BackupPath);
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            throw new InvalidOperationException("Backup path is required.");
        }

        if (request.RetentionCount <= 0 || request.RetentionCount > 200)
        {
            throw new InvalidOperationException("Retention count must be between 1 and 200.");
        }

        if (request.ScheduleDayOfWeek < 0 || request.ScheduleDayOfWeek > 6)
        {
            throw new InvalidOperationException("Invalid day of week.");
        }

        if (!TimeSpan.TryParse(request.ScheduleTime, out _))
        {
            throw new InvalidOperationException("Invalid schedule time.");
        }

        var pgBinPath = NormalizePgBinPathForRuntime(request.PgBinPath);
        if (string.IsNullOrWhiteSpace(pgBinPath))
        {
            throw new InvalidOperationException("pg_bin_path is required.");
        }

        var dumpTool = ResolvePgToolPath(pgBinPath, "pg_dump");
        var restoreTool = ResolvePgToolPath(pgBinPath, "pg_restore");
        if (!ToolPathLooksValid(dumpTool) || !ToolPathLooksValid(restoreTool))
        {
            throw new InvalidOperationException("pg_bin_path is invalid.");
        }

        Directory.CreateDirectory(backupPath);

        settings.Enabled = request.Enabled;
        settings.BackupPath = backupPath;
        settings.RetentionCount = request.RetentionCount;
        settings.ScheduleDayOfWeek = request.ScheduleDayOfWeek;
        settings.ScheduleTime = request.ScheduleTime.Trim();
        settings.PgBinPath = pgBinPath;
        settings.Timezone = TimeZoneInfo.Local.Id;

        await _db.SaveChangesAsync(ct);

        await WriteAuditAsync("settings_update", "success", new
        {
            settings.Enabled,
            settings.BackupPath,
            settings.RetentionCount,
            settings.ScheduleDayOfWeek,
            settings.ScheduleTime,
            settings.PgBinPath
        }, ct);

        return MapSettings(settings);
    }

    public async Task<BackupJobListItem> EnqueueManualBackupAsync(CancellationToken ct)
    {
        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            Type = "manual",
            Status = "queued",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        _db.BackupJobs.Add(job);
        await _db.SaveChangesAsync(ct);
        _queue.Enqueue(job.Id);

        await WriteAuditAsync("backup_manual", "success", new { job.Id, job.Status }, ct);

        return MapJobListItem(job);
    }

    public async Task<bool> HasPendingScheduledBackupAsync(CancellationToken ct)
    {
        return await _db.BackupJobs.AnyAsync(
            job => job.Type == "scheduled" && (job.Status == "queued" || job.Status == "running"),
            ct);
    }

    public async Task EnqueueScheduledBackupAsync(CancellationToken ct)
    {
        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            Type = "scheduled",
            Status = "queued",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.BackupJobs.Add(job);
        await _db.SaveChangesAsync(ct);
        _queue.Enqueue(job.Id);

        await WriteAuditAsync("backup_scheduled", "success", new { job.Id, job.Status }, ct);
    }

    public async Task ProcessJobAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _db.BackupJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null || job.Status != "queued")
        {
            return;
        }

        if (!await TryAcquireAdvisoryLockAsync(BackupLockKey, ct))
        {
            _logger.LogInformation("Backup job {JobId} skipped due to advisory lock.", jobId);
            job.Status = "skipped";
            job.FinishedAt = DateTimeOffset.UtcNow;
            job.ErrorMessage = "Skipped due to advisory lock.";

            if (job.Type == "scheduled")
            {
                var lockSettings = await GetOrCreateSettingsAsync(ct);
                lockSettings.LastRunAt = DateTimeOffset.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
            await WriteAuditAsync("backup_" + job.Type, "skipped", new { job.Id, job.Status, job.ErrorMessage }, ct);
            return;
        }

        var settings = await GetOrCreateSettingsAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var fileName = $"congno_golden_{now:yyyyMMdd_HHmm}.dump";
        var backupPath = settings.BackupPath;
        Directory.CreateDirectory(backupPath);
        var filePath = Path.Combine(backupPath, fileName);

        job.Status = "running";
        job.StartedAt = now;
        await _db.SaveChangesAsync(ct);

        BackupProcessResult result;
        try
        {
            result = await RunPgDumpAsync(settings, filePath, ct);
        }
        catch (Exception ex)
        {
            job.Status = "failed";
            job.FinishedAt = DateTimeOffset.UtcNow;
            job.ErrorMessage = ex.Message;
            job.StderrLog = Truncate(ex.ToString());
            await _db.SaveChangesAsync(ct);
            await WriteAuditAsync("backup_" + job.Type, "failed", new { job.Id, ex.Message }, ct);
            await NotifyBackupFailureAsync(job, ct);
            return;
        }

        job.StdoutLog = Truncate(result.Stdout);
        job.StderrLog = Truncate(result.Stderr);

        if (result.ExitCode == 0)
        {
            job.Status = "success";
            job.FinishedAt = DateTimeOffset.UtcNow;
            job.FileName = fileName;
            job.FilePath = filePath;
            if (File.Exists(filePath))
            {
                job.FileSize = new FileInfo(filePath).Length;
            }
        }
        else
        {
            job.Status = "failed";
            job.FinishedAt = DateTimeOffset.UtcNow;
            job.ErrorMessage = $"pg_dump exit code {result.ExitCode}";
        }

        if (job.Type == "scheduled")
        {
            settings.LastRunAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        await WriteAuditAsync("backup_" + job.Type, job.Status == "success" ? "success" : "failed", new
        {
            job.Id,
            job.Status,
            job.ErrorMessage
        }, ct);

        if (job.Status == "success")
        {
            await ApplyRetentionAsync(settings.RetentionCount, ct);
        }
        else
        {
            await NotifyBackupFailureAsync(job, ct);
        }
    }

    public async Task<PagedResult<BackupJobListItem>> ListJobsAsync(BackupJobQuery query, CancellationToken ct)
    {
        var pageValue = query.Page <= 0 ? 1 : query.Page;
        var sizeValue = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 200);

        var jobsQuery = _db.BackupJobs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToLowerInvariant();
            jobsQuery = jobsQuery.Where(j => j.Status.ToLower() == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var type = query.Type.Trim().ToLowerInvariant();
            jobsQuery = jobsQuery.Where(j => j.Type.ToLower() == type);
        }

        var total = await jobsQuery.CountAsync(ct);
        var items = await jobsQuery
            .OrderByDescending(j => j.CreatedAt)
            .Skip((pageValue - 1) * sizeValue)
            .Take(sizeValue)
            .Select(j => MapJobListItem(j))
            .ToListAsync(ct);

        return new PagedResult<BackupJobListItem>(items, pageValue, sizeValue, total);
    }

    public async Task<BackupJobDetail?> GetJobAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _db.BackupJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);
        return job is null ? null : MapJobDetail(job);
    }

    public async Task<BackupDownloadToken> IssueDownloadTokenAsync(
        Guid jobId,
        DateTimeOffset now,
        TimeSpan ttl,
        CancellationToken ct)
    {
        var job = await _db.BackupJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null || job.Status != "success" || string.IsNullOrWhiteSpace(job.FilePath))
        {
            throw new InvalidOperationException("Backup job is not ready for download.");
        }
        if (!File.Exists(job.FilePath))
        {
            throw new InvalidOperationException("Backup file not found.");
        }

        var token = BackupDownloadTokenGenerator.CreateToken(now, ttl == default ? DefaultTokenTtl : ttl);
        job.DownloadToken = token.Token;
        job.DownloadTokenExpiresAt = token.ExpiresAt;
        await _db.SaveChangesAsync(ct);

        await WriteAuditAsync("download", "success", new { job.Id }, ct);

        return token;
    }

    public async Task<Stream?> OpenDownloadStreamAsync(Guid jobId, string token, DateTimeOffset now, CancellationToken ct)
    {
        var job = await _db.BackupJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null || job.Status != "success" || string.IsNullOrWhiteSpace(job.FilePath))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(job.DownloadToken) ||
            !string.Equals(job.DownloadToken, token, StringComparison.Ordinal) ||
            job.DownloadTokenExpiresAt is null ||
            job.DownloadTokenExpiresAt < now)
        {
            return null;
        }

        if (!File.Exists(job.FilePath))
        {
            return null;
        }

        return new FileStream(job.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public async Task<BackupUploadResult> UploadAsync(string fileName, long fileSize, Stream stream, CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(ct);
        var uploadsPath = Path.Combine(settings.BackupPath, "uploads");
        Directory.CreateDirectory(uploadsPath);

        var uploadId = Guid.NewGuid();
        var safeFileName = Path.GetFileName(fileName);
        var targetPath = Path.Combine(uploadsPath, $"{uploadId}_{safeFileName}");

        await using var file = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(file, ct);

        var upload = new BackupUpload
        {
            Id = uploadId,
            FileName = safeFileName,
            FileSize = fileSize,
            FilePath = targetPath,
            CreatedBy = _currentUser.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(UploadTtl)
        };

        _db.BackupUploads.Add(upload);
        await _db.SaveChangesAsync(ct);

        await WriteAuditAsync("upload", "success", new { upload.Id }, ct);

        return new BackupUploadResult(upload.Id, upload.FileName, upload.FileSize, upload.ExpiresAt);
    }

    public async Task RestoreAsync(BackupRestoreRequest request, CancellationToken ct)
    {
        if (!string.Equals(request.ConfirmPhrase?.Trim(), "RESTORE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Confirm phrase is required.");
        }

        var filePath = await ResolveRestoreFileAsync(request, ct);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Restore file not found.");
        }

        if (!await TryAcquireAdvisoryLockAsync(RestoreLockKey, ct))
        {
            throw new InvalidOperationException("Restore is already running.");
        }

        var settings = await GetOrCreateSettingsAsync(ct);

        _maintenanceState.SetActive(true, "He thong dang phuc hoi du lieu.");
        try
        {
            var result = await RunPgRestoreAsync(settings, filePath, ct);
            var auditResult = result.ExitCode == 0 ? "success" : "failed";
            await WriteAuditAsync("restore", auditResult, new
            {
                result.ExitCode,
                Stdout = Truncate(result.Stdout),
                Stderr = Truncate(result.Stderr)
            }, ct);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(BuildRestoreFailureMessage(result));
            }

            ApplyMigrationsAfterRestore();
        }
        finally
        {
            _maintenanceState.SetActive(false);
        }
    }

    public async Task<PagedResult<BackupAuditItem>> ListAuditAsync(int page, int pageSize, CancellationToken ct)
    {
        var pageValue = page <= 0 ? 1 : page;
        var sizeValue = pageSize <= 0 ? 20 : Math.Min(pageSize, 200);

        var query = _db.BackupAudits.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((pageValue - 1) * sizeValue)
            .Take(sizeValue)
            .Select(a => new BackupAuditItem(
                a.Id,
                a.Action,
                a.ActorId,
                a.Result,
                a.Details,
                a.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<BackupAuditItem>(items, pageValue, sizeValue, total);
    }

    public Task<bool> IsMaintenanceModeAsync(CancellationToken ct)
    {
        return Task.FromResult(_maintenanceState.IsActive);
    }

    private async Task<BackupSettings> GetOrCreateSettingsAsync(CancellationToken ct)
    {
        var settings = await _db.BackupSettings.FirstOrDefaultAsync(ct);
        if (settings is not null)
        {
            var normalizedBackupPath = NormalizeBackupPathForRuntime(settings.BackupPath);
            var normalizedPgBinPath = NormalizePgBinPathForRuntime(settings.PgBinPath);
            var changed = false;

            if (!string.Equals(settings.BackupPath, normalizedBackupPath, StringComparison.Ordinal))
            {
                settings.BackupPath = normalizedBackupPath;
                changed = true;
            }

            if (!string.Equals(settings.PgBinPath, normalizedPgBinPath, StringComparison.Ordinal))
            {
                settings.PgBinPath = normalizedPgBinPath;
                changed = true;
            }

            if (changed)
            {
                settings.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            return settings;
        }

        settings = new BackupSettings
        {
            Id = Guid.NewGuid(),
            Enabled = false,
            BackupPath = GetDefaultBackupPath(),
            RetentionCount = 10,
            ScheduleDayOfWeek = (int)DayOfWeek.Monday,
            ScheduleTime = "02:00",
            Timezone = TimeZoneInfo.Local.Id,
            PgBinPath = GetDefaultPgBinPath(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.BackupSettings.Add(settings);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    private BackupSettingsDto MapSettings(BackupSettings settings)
    {
        return new BackupSettingsDto(
            settings.Enabled,
            settings.BackupPath,
            settings.RetentionCount,
            settings.ScheduleDayOfWeek,
            settings.ScheduleTime,
            settings.Timezone,
            settings.PgBinPath,
            settings.LastRunAt);
    }

    private static BackupJobListItem MapJobListItem(BackupJob job)
    {
        return new BackupJobListItem(
            job.Id,
            job.Type,
            job.Status,
            job.CreatedAt,
            job.StartedAt,
            job.FinishedAt,
            job.FileName,
            job.FileSize,
            job.ErrorMessage,
            job.CreatedBy);
    }

    private static BackupJobDetail MapJobDetail(BackupJob job)
    {
        return new BackupJobDetail(
            job.Id,
            job.Type,
            job.Status,
            job.CreatedAt,
            job.StartedAt,
            job.FinishedAt,
            job.FileName,
            job.FileSize,
            job.ErrorMessage,
            job.StdoutLog,
            job.StderrLog,
            job.DownloadTokenExpiresAt,
            job.CreatedBy);
    }

    private async Task ApplyRetentionAsync(int retentionCount, CancellationToken ct)
    {
        var jobs = await _db.BackupJobs
            .AsNoTracking()
            .Where(j => j.Status == "success" && j.FinishedAt != null)
            .Select(j => new BackupRetentionJob(j.Id, j.FinishedAt!.Value))
            .ToListAsync(ct);

        var expired = BackupRetentionPolicy.SelectExpiredJobs(jobs, retentionCount);
        if (expired.Count == 0)
        {
            return;
        }

        var deleteJobs = await _db.BackupJobs
            .Where(j => expired.Contains(j.Id))
            .ToListAsync(ct);

        foreach (var job in deleteJobs)
        {
            if (!string.IsNullOrWhiteSpace(job.FilePath) && File.Exists(job.FilePath))
            {
                try
                {
                    File.Delete(job.FilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete backup file {FilePath}", job.FilePath);
                }
            }
        }

        _db.BackupJobs.RemoveRange(deleteJobs);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<BackupProcessResult> RunPgDumpAsync(BackupSettings settings, string filePath, CancellationToken ct)
    {
        var connection = GetMigrationConnectionString();
        var builder = new NpgsqlConnectionStringBuilder(connection);
        var exePath = ResolvePgToolPath(settings.PgBinPath, "pg_dump");
        if (!ToolPathLooksValid(exePath))
        {
            throw new InvalidOperationException("pg_dump not found.");
        }
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            throw new InvalidOperationException("Database name is required.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = BuildPgDumpArguments(builder, filePath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(builder.Password))
        {
            startInfo.Environment["PGPASSWORD"] = builder.Password;
        }

        return await _processRunner.RunAsync(startInfo, ct);
    }

    private async Task<BackupProcessResult> RunPgRestoreAsync(BackupSettings settings, string filePath, CancellationToken ct)
    {
        var connection = GetMigrationConnectionString();
        var builder = new NpgsqlConnectionStringBuilder(connection);
        await ResetSchemaBeforeRestoreAsync(builder, ct);
        var exePath = ResolvePgToolPath(settings.PgBinPath, "pg_restore");
        if (!ToolPathLooksValid(exePath))
        {
            throw new InvalidOperationException("pg_restore not found.");
        }
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            throw new InvalidOperationException("Database name is required.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = BuildPgRestoreArguments(builder, filePath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(builder.Password))
        {
            startInfo.Environment["PGPASSWORD"] = builder.Password;
        }

        return await _processRunner.RunAsync(startInfo, ct);
    }

    private static string BuildPgDumpArguments(NpgsqlConnectionStringBuilder builder, string filePath)
    {
        return $"-h {builder.Host} -p {builder.Port} -U {builder.Username} -F c -b -O -x -f \"{filePath}\" {builder.Database}";
    }

    private static string BuildPgRestoreArguments(NpgsqlConnectionStringBuilder builder, string filePath)
    {
        return $"-h {builder.Host} -p {builder.Port} -U {builder.Username} -d {builder.Database} --clean --if-exists --no-owner --no-privileges --exit-on-error \"{filePath}\"";
    }

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Length <= LogLimit ? value : value[..LogLimit];
    }

    private static string BuildRestoreFailureMessage(BackupProcessResult result)
    {
        var stderr = Truncate(result.Stderr);
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            return $"pg_restore failed with exit code {result.ExitCode}. {AppendRestoreHint(stderr)}";
        }

        var stdout = Truncate(result.Stdout);
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            return $"pg_restore failed with exit code {result.ExitCode}. {AppendRestoreHint(stdout)}";
        }

        return $"pg_restore failed with exit code {result.ExitCode}.";
    }

    private static string AppendRestoreHint(string message)
    {
        if (message.Contains("must be owner", StringComparison.OrdinalIgnoreCase))
        {
            return $"{message} Hãy cấu hình ConnectionStrings__Migrations với tài khoản DB owner/superuser để phục hồi.";
        }

        return message;
    }

    private string GetDefaultBackupPath()
    {
        var configured = _configuration["Backup:DefaultPath"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return OperatingSystem.IsWindows()
            ? WindowsDefaultBackupPath
            : LinuxDefaultBackupPath;
    }

    private string GetDefaultPgBinPath()
    {
        var configured = _configuration["Backup:DefaultPgBinPath"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return OperatingSystem.IsWindows()
            ? WindowsDefaultPgBinPath
            : LinuxDefaultPgBinPath;
    }

    private string NormalizeBackupPathForRuntime(string? rawPath)
    {
        var candidate = string.IsNullOrWhiteSpace(rawPath) ? GetDefaultBackupPath() : rawPath.Trim();
        if (!OperatingSystem.IsWindows() && IsWindowsStylePath(candidate))
        {
            return GetDefaultBackupPath();
        }

        return candidate;
    }

    private string NormalizePgBinPathForRuntime(string? rawPath)
    {
        var candidate = string.IsNullOrWhiteSpace(rawPath) ? GetDefaultPgBinPath() : rawPath.Trim();
        if (!OperatingSystem.IsWindows() && IsWindowsStylePath(candidate))
        {
            return GetDefaultPgBinPath();
        }

        return candidate;
    }

    private static string ResolvePgToolPath(string pgBinPath, string toolBaseName)
    {
        var executableName = OperatingSystem.IsWindows() ? $"{toolBaseName}.exe" : toolBaseName;
        if (string.IsNullOrWhiteSpace(pgBinPath))
        {
            return executableName;
        }

        return Path.Combine(pgBinPath, executableName);
    }

    private static bool ToolPathLooksValid(string toolPath)
    {
        if (string.IsNullOrWhiteSpace(toolPath))
        {
            return false;
        }

        if (Path.IsPathRooted(toolPath))
        {
            return File.Exists(toolPath);
        }

        return true;
    }

    private static bool IsWindowsStylePath(string value)
    {
        return value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':';
    }

}
