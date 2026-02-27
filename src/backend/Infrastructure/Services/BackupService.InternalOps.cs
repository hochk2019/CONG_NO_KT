using System.Data;
using System.Text.Json;
using CongNoGolden.Application.Backups;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class BackupService
{
    private async Task ResetSchemaBeforeRestoreAsync(NpgsqlConnectionStringBuilder builder, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = $"DROP SCHEMA IF EXISTS {DefaultSchema} CASCADE;";

        try
        {
            await command.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Không thể dọn schema {DefaultSchema} trước khi restore. Hãy kiểm tra quyền DB của ConnectionStrings__Migrations.",
                ex);
        }
    }

    private void ApplyMigrationsAfterRestore()
    {
        var mergedConfiguration = new ConfigurationBuilder()
            .AddConfiguration(_configuration)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Migrations:Enabled"] = "true"
            })
            .Build();

        try
        {
            MigrationRunner.ApplyMigrations(mergedConfiguration, _logger);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Phục hồi dữ liệu thành công nhưng nâng cấp schema thất bại. Hãy kiểm tra scripts migration và ConnectionStrings__Migrations.",
                ex);
        }
    }

    private string GetMigrationConnectionString()
    {
        var connection = _configuration.GetConnectionString("Migrations");
        if (string.IsNullOrWhiteSpace(connection))
        {
            connection = _configuration.GetConnectionString("Default");
        }

        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException("Connection string is not configured.");
        }

        return connection;
    }

    private async Task WriteAuditAsync(string action, string result, object details, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(details);
        var audit = new BackupAudit
        {
            Id = Guid.NewGuid(),
            Action = action,
            ActorId = _currentUser.UserId,
            Result = result,
            Details = payload,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.BackupAudits.Add(audit);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<bool> TryAcquireAdvisoryLockAsync(string key, CancellationToken ct)
    {
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(hashtext(@key))", connection);
        command.Parameters.AddWithValue("key", key);
        var result = await command.ExecuteScalarAsync(ct);
        if (shouldClose)
        {
            await connection.CloseAsync();
        }

        return result is bool acquired && acquired;
    }

    private async Task<string?> ResolveRestoreFileAsync(BackupRestoreRequest request, CancellationToken ct)
    {
        if (request.JobId is Guid jobId)
        {
            var job = await _db.BackupJobs.AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == jobId, ct);
            if (job?.Status == "success" && !string.IsNullOrWhiteSpace(job.FilePath))
            {
                return job.FilePath;
            }
        }

        if (request.UploadId is Guid uploadId)
        {
            var upload = await _db.BackupUploads.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == uploadId, ct);
            if (upload is null || upload.ExpiresAt < DateTimeOffset.UtcNow)
            {
                return null;
            }
            return upload.FilePath;
        }

        return null;
    }

    private async Task NotifyBackupFailureAsync(BackupJob job, CancellationToken ct)
    {
        var recipients = new HashSet<Guid>();

        if (job.CreatedBy.HasValue)
        {
            recipients.Add(job.CreatedBy.Value);
        }
        else
        {
            var adminIds = await LoadAdminSupervisorIdsAsync(ct);
            foreach (var adminId in adminIds)
            {
                recipients.Add(adminId);
            }
        }

        var allowedRecipients = await FilterRecipientsAsync(recipients, ct);
        if (allowedRecipients.Count == 0)
        {
            return;
        }

        var metadata = JsonSerializer.Serialize(new
        {
            jobId = job.Id,
            jobType = job.Type,
            status = job.Status,
            errorMessage = job.ErrorMessage
        });

        var body = string.IsNullOrWhiteSpace(job.ErrorMessage)
            ? "Sao lưu dữ liệu thất bại."
            : $"Sao lưu dữ liệu thất bại: {job.ErrorMessage}";
        var now = DateTimeOffset.UtcNow;

        foreach (var userId in allowedRecipients)
        {
            _db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = "Sao lưu thất bại",
                Body = body,
                Severity = "ALERT",
                Source = "SYSTEM",
                Metadata = metadata,
                CreatedAt = now
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<Guid>> LoadAdminSupervisorIdsAsync(CancellationToken ct)
    {
        return await _db.UserRoles
            .Join(_db.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Code })
            .Where(r => r.Code == "Admin" || r.Code == "Supervisor")
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<Guid>> FilterRecipientsAsync(
        IReadOnlySet<Guid> recipients,
        CancellationToken ct)
    {
        if (recipients.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var ids = recipients.ToArray();
        var existingUsers = await _db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(ct);
        if (existingUsers.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var disabled = await _db.NotificationPreferences
            .AsNoTracking()
            .Where(p => existingUsers.Contains(p.UserId) && !p.ReceiveNotifications)
            .Select(p => p.UserId)
            .ToListAsync(ct);

        return existingUsers.Where(id => !disabled.Contains(id)).ToList();
    }
}
