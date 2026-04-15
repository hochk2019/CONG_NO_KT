using System.Net;
using System.Text;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class GoogleDriveBackupOffsiteServiceTests
{
    [Fact]
    public async Task BuildGoogleDriveConnectUrlAsync_WhenConfigured_ReturnsExpectedUrl()
    {
        await using var db = CreateDbContext();
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("HTTP should not be called.")));
        var service = CreateService(db, httpClient);

        var url = await service.BuildGoogleDriveConnectUrlAsync(
            "https://localhost/api/backups/offsite/google-drive/callback",
            "folder-123",
            CancellationToken.None);

        Assert.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?", url, StringComparison.Ordinal);
        Assert.Contains("client_id=test-client-id", url, StringComparison.Ordinal);
        Assert.Contains("response_type=code", url, StringComparison.Ordinal);
        Assert.Contains("access_type=offline", url, StringComparison.Ordinal);
        Assert.Contains("prompt=consent", url, StringComparison.Ordinal);
        Assert.Contains(
            "redirect_uri=https%3A%2F%2Flocalhost%2Fapi%2Fbackups%2Foffsite%2Fgoogle-drive%2Fcallback",
            url,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteGoogleDriveCallbackAsync_StoresEncryptedRefreshTokenAndFolder()
    {
        await using var db = CreateDbContext();
        using var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal("https://oauth2.googleapis.com/token", request.RequestUri?.ToString());
            return Task.FromResult(CreateJsonResponse("""
                {
                  "access_token": "access-123",
                  "refresh_token": "refresh-456"
                }
                """));
        }));

        var service = CreateService(db, httpClient);

        var status = await service.CompleteGoogleDriveCallbackAsync(
            "code-123",
            "https://localhost/api/backups/offsite/google-drive/callback",
            "folder-abc",
            CancellationToken.None);

        Assert.True(status.IsConnected);
        Assert.Equal("google_drive", status.Provider);
        Assert.Equal("folder-abc", status.GoogleDriveFolderId);

        var entity = await db.BackupOffsiteConnections.SingleAsync();
        Assert.Equal("google_drive", entity.Provider);
        Assert.Equal("connected", entity.Status);
        Assert.Equal("folder-abc", entity.GoogleDriveFolderId);
        Assert.NotNull(entity.EncryptedRefreshToken);
        Assert.NotEqual("refresh-456", entity.EncryptedRefreshToken);
    }

    [Fact]
    public async Task ProcessNextPendingUploadAsync_WhenQueuedBackupExists_UploadsAndMarksSuccess()
    {
        await using var db = CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var backupFilePath = Path.Combine(Path.GetTempPath(), $"congno-drive-success-{Guid.NewGuid():N}.bak");
        await File.WriteAllTextAsync(backupFilePath, "backup-payload", CancellationToken.None);

        try
        {
            var dataProtectionProvider = new EphemeralDataProtectionProvider();
            var protector = dataProtectionProvider
                .CreateProtector("CongNoGolden.Backups.GoogleDrive.RefreshToken");

            var settings = new BackupSettings
            {
                Id = Guid.NewGuid(),
                Enabled = true,
                BackupPath = Path.GetDirectoryName(backupFilePath)!,
                RetentionCount = 7,
                ScheduleDayOfWeek = 1,
                ScheduleTime = "02:00",
                Timezone = "UTC",
                PgBinPath = "C:\\Program Files\\PostgreSQL\\16\\bin",
                GoogleDriveFolderId = "settings-folder",
                CreatedAt = now,
                UpdatedAt = now
            };

            var connection = new BackupOffsiteConnection
            {
                Id = Guid.NewGuid(),
                Provider = "google_drive",
                Status = "connected",
                EncryptedRefreshToken = protector.Protect("refresh-token"),
                GoogleDriveFolderId = null,
                ConnectedAt = now,
                LastValidatedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };

            var job = new BackupJob
            {
                Id = Guid.NewGuid(),
                Type = "manual",
                Status = "success",
                FileName = "backup-success.bak",
                FilePath = backupFilePath,
                CreatedAt = now
            };

            var upload = new BackupOffsiteUpload
            {
                Id = Guid.NewGuid(),
                BackupJobId = job.Id,
                Provider = "google_drive",
                Status = "queued",
                AttemptCount = 0,
                CreatedAt = now,
                QueuedAt = now,
                UpdatedAt = now
            };

            db.BackupSettings.Add(settings);
            db.BackupOffsiteConnections.Add(connection);
            db.BackupJobs.Add(job);
            db.BackupOffsiteUploads.Add(upload);
            await db.SaveChangesAsync();

            using var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
            {
                // Token refresh
                if (request.RequestUri?.ToString() == "https://oauth2.googleapis.com/token")
                {
                    return Task.FromResult(CreateJsonResponse("""
                        {
                          "access_token": "access-xyz"
                        }
                        """));
                }

                // Initiate resumable session: POST to upload endpoint
                if (request.Method == HttpMethod.Post &&
                    request.RequestUri?.ToString()?.Contains("uploadType=resumable") == true)
                {
                    Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                    Assert.Equal("access-xyz", request.Headers.Authorization?.Parameter);
                    var resp = new HttpResponseMessage(HttpStatusCode.OK);
                    resp.Headers.Location = new Uri("https://upload.googleapis.com/session/test-session-001");
                    return Task.FromResult(resp);
                }

                // Upload chunk: PUT to session URI
                if (request.Method == HttpMethod.Put &&
                    request.RequestUri?.ToString() == "https://upload.googleapis.com/session/test-session-001")
                {
                    // Status query (Content-Length = 0) → 308 with no Range (nothing uploaded yet)
                    if (request.Content?.Headers.ContentLength == 0)
                    {
                        return Task.FromResult(new HttpResponseMessage((HttpStatusCode)308));
                    }

                    // Actual data chunk → success
                    return Task.FromResult(CreateJsonResponse("""
                        {
                          "id": "file-001",
                          "size": "14",
                          "md5Checksum": "abc123"
                        }
                        """));
                }

                throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
            }));

            var service = CreateService(db, httpClient, dataProtectionProvider: dataProtectionProvider);
            var processed = await service.ProcessNextPendingUploadAsync(CancellationToken.None);

            Assert.True(processed);

            var refreshedUpload = await db.BackupOffsiteUploads.SingleAsync(x => x.Id == upload.Id);
            if (refreshedUpload.Status != "success")
                throw new Exception($"Upload failed! Error Message: {refreshedUpload.ErrorMessage}");
            Assert.Equal("success", refreshedUpload.Status);
            Assert.Equal("file-001", refreshedUpload.RemoteFileId);
            Assert.Equal("abc123", refreshedUpload.RemoteChecksum);
            Assert.Equal(14L, refreshedUpload.RemoteFileSize);
            Assert.NotNull(refreshedUpload.CompletedAt);

            var refreshedConnection = await db.BackupOffsiteConnections.SingleAsync(x => x.Id == connection.Id);
            Assert.Equal("connected", refreshedConnection.Status);
            Assert.NotNull(refreshedConnection.LastValidatedAt);
            Assert.Null(refreshedConnection.LastError);
        }
        finally
        {
            if (File.Exists(backupFilePath))
            {
                File.Delete(backupFilePath);
            }
        }
    }

    [Fact]
    public async Task ProcessNextPendingUploadAsync_WhenBackupFileMissing_MarksFailed()
    {
        await using var db = CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var missingFilePath = Path.Combine(Path.GetTempPath(), $"congno-drive-missing-{Guid.NewGuid():N}.bak");

        var protector = new EphemeralDataProtectionProvider()
            .CreateProtector("CongNoGolden.Backups.GoogleDrive.RefreshToken");

        db.BackupSettings.Add(new BackupSettings
        {
            Id = Guid.NewGuid(),
            Enabled = true,
            BackupPath = Path.GetTempPath(),
            RetentionCount = 7,
            ScheduleDayOfWeek = 1,
            ScheduleTime = "02:00",
            Timezone = "UTC",
            PgBinPath = "C:\\Program Files\\PostgreSQL\\16\\bin",
            GoogleDriveFolderId = "settings-folder",
            CreatedAt = now,
            UpdatedAt = now
        });

        db.BackupOffsiteConnections.Add(new BackupOffsiteConnection
        {
            Id = Guid.NewGuid(),
            Provider = "google_drive",
            Status = "connected",
            EncryptedRefreshToken = protector.Protect("refresh-token"),
            ConnectedAt = now,
            LastValidatedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });

        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            Type = "manual",
            Status = "success",
            FileName = "backup-missing.bak",
            FilePath = missingFilePath,
            CreatedAt = now
        };

        var upload = new BackupOffsiteUpload
        {
            Id = Guid.NewGuid(),
            BackupJobId = job.Id,
            Provider = "google_drive",
            Status = "queued",
            AttemptCount = 0,
            CreatedAt = now,
            QueuedAt = now,
            UpdatedAt = now
        };

        db.BackupJobs.Add(job);
        db.BackupOffsiteUploads.Add(upload);
        await db.SaveChangesAsync();

        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("HTTP should not be called when backup file is missing.")));

        var service = CreateService(db, httpClient);
        var processed = await service.ProcessNextPendingUploadAsync(CancellationToken.None);

        Assert.True(processed);

        var refreshedUpload = await db.BackupOffsiteUploads.SingleAsync(x => x.Id == upload.Id);
        Assert.Equal("failed", refreshedUpload.Status);
        Assert.Contains("Backup file was not found", refreshedUpload.ErrorMessage, StringComparison.Ordinal);
        Assert.Null(refreshedUpload.CompletedAt);
    }

    [Fact]
    public async Task ProcessNextPendingUploadAsync_WhenUploadFails_SchedulesRetryInsteadOfPermanentFail()
    {
        await using var db = CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var backupFilePath = Path.Combine(Path.GetTempPath(), $"congno-drive-retry-{Guid.NewGuid():N}.bak");
        await File.WriteAllTextAsync(backupFilePath, "backup-payload", CancellationToken.None);

        try
        {
            var protector = new EphemeralDataProtectionProvider()
                .CreateProtector("CongNoGolden.Backups.GoogleDrive.RefreshToken");

            db.BackupSettings.Add(new BackupSettings
            {
                Id = Guid.NewGuid(), Enabled = true, BackupPath = Path.GetTempPath(),
                RetentionCount = 7, ScheduleDayOfWeek = 1, ScheduleTime = "02:00",
                Timezone = "UTC", PgBinPath = "/usr/bin",
                CreatedAt = now, UpdatedAt = now
            });
            db.BackupOffsiteConnections.Add(new BackupOffsiteConnection
            {
                Id = Guid.NewGuid(), Provider = "google_drive", Status = "connected",
                EncryptedRefreshToken = protector.Protect("refresh-token"),
                ConnectedAt = now, LastValidatedAt = now, CreatedAt = now, UpdatedAt = now
            });
            var job = new BackupJob
            {
                Id = Guid.NewGuid(), Type = "manual", Status = "success",
                FileName = "retry-test.bak", FilePath = backupFilePath, CreatedAt = now
            };
            var upload = new BackupOffsiteUpload
            {
                Id = Guid.NewGuid(), BackupJobId = job.Id, Provider = "google_drive",
                Status = "queued", AttemptCount = 0, MaxAttempts = 5,
                CreatedAt = now, QueuedAt = now, UpdatedAt = now
            };
            db.BackupJobs.Add(job);
            db.BackupOffsiteUploads.Add(upload);
            await db.SaveChangesAsync();

            using var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
            {
                // Token refresh succeeds
                if (request.RequestUri?.ToString() == "https://oauth2.googleapis.com/token")
                {
                    return Task.FromResult(CreateJsonResponse("""{ "access_token": "access-xyz" }"""));
                }

                // Initiate resumable session → 200 with Location header (session URI)
                if (request.Method == HttpMethod.Post &&
                    request.RequestUri?.ToString()?.Contains("uploadType=resumable") == true)
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.OK);
                    resp.Headers.Location = new Uri("https://upload.googleapis.com/session/abc");
                    return Task.FromResult(resp);
                }

                // Resume query (PUT * bytes) → 308 with no Range header (nothing received yet)
                if (request.Method == HttpMethod.Put &&
                    request.Headers.Contains("Content-Range") &&
                    request.RequestUri?.ToString() == "https://upload.googleapis.com/session/abc")
                {
                    // Simulate the actual chunk upload failing with 503
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                }

                throw new InvalidOperationException($"Unexpected: {request.Method} {request.RequestUri}");
            }));

            var service = CreateService(db, httpClient);
            var processed = await service.ProcessNextPendingUploadAsync(CancellationToken.None);

            Assert.True(processed);
            var refreshedUpload = await db.BackupOffsiteUploads.SingleAsync(x => x.Id == upload.Id);

            // Should schedule a retry, NOT permanently fail
            Assert.Equal("queued", refreshedUpload.Status);
            Assert.Equal(1, refreshedUpload.AttemptCount);
            Assert.NotNull(refreshedUpload.NextRetryAt);
            Assert.True(refreshedUpload.NextRetryAt > now);
            Assert.Null(refreshedUpload.CompletedAt);
        }
        finally
        {
            if (File.Exists(backupFilePath)) File.Delete(backupFilePath);
        }
    }

    [Fact]
    public async Task ProcessNextPendingUploadAsync_WhenMaxAttemptsExceeded_PermanentlyFails()
    {
        await using var db = CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var backupFilePath = Path.Combine(Path.GetTempPath(), $"congno-drive-maxattempt-{Guid.NewGuid():N}.bak");
        await File.WriteAllTextAsync(backupFilePath, "backup-payload", CancellationToken.None);

        try
        {
            var protector = new EphemeralDataProtectionProvider()
                .CreateProtector("CongNoGolden.Backups.GoogleDrive.RefreshToken");

            db.BackupSettings.Add(new BackupSettings
            {
                Id = Guid.NewGuid(), Enabled = true, BackupPath = Path.GetTempPath(),
                RetentionCount = 7, ScheduleDayOfWeek = 1, ScheduleTime = "02:00",
                Timezone = "UTC", PgBinPath = "/usr/bin",
                CreatedAt = now, UpdatedAt = now
            });
            db.BackupOffsiteConnections.Add(new BackupOffsiteConnection
            {
                Id = Guid.NewGuid(), Provider = "google_drive", Status = "connected",
                EncryptedRefreshToken = protector.Protect("refresh-token"),
                ConnectedAt = now, LastValidatedAt = now, CreatedAt = now, UpdatedAt = now
            });
            var job = new BackupJob
            {
                Id = Guid.NewGuid(), Type = "manual", Status = "success",
                FileName = "maxattempt-test.bak", FilePath = backupFilePath, CreatedAt = now
            };
            // AttemptCount already at MaxAttempts - next failure should permanently fail
            var upload = new BackupOffsiteUpload
            {
                Id = Guid.NewGuid(), BackupJobId = job.Id, Provider = "google_drive",
                Status = "queued", AttemptCount = 5, MaxAttempts = 5,
                CreatedAt = now, QueuedAt = now, UpdatedAt = now
            };
            db.BackupJobs.Add(job);
            db.BackupOffsiteUploads.Add(upload);
            await db.SaveChangesAsync();

            using var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
            {
                if (request.RequestUri?.ToString() == "https://oauth2.googleapis.com/token")
                    return Task.FromResult(CreateJsonResponse("""{ "access_token": "access-xyz" }"""));

                if (request.Method == HttpMethod.Post &&
                    request.RequestUri?.ToString()?.Contains("uploadType=resumable") == true)
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.OK);
                    resp.Headers.Location = new Uri("https://upload.googleapis.com/session/fail");
                    return Task.FromResult(resp);
                }

                // Any put → force fail
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }));

            var service = CreateService(db, httpClient);
            await service.ProcessNextPendingUploadAsync(CancellationToken.None);

            var refreshedUpload = await db.BackupOffsiteUploads.SingleAsync(x => x.Id == upload.Id);
            Assert.Equal("failed", refreshedUpload.Status);
            Assert.Null(refreshedUpload.NextRetryAt);
            Assert.Null(refreshedUpload.ResumableSessionUri);
        }
        finally
        {
            if (File.Exists(backupFilePath)) File.Delete(backupFilePath);
        }
    }

    [Fact]
    public async Task ProcessNextPendingUploadAsync_WhenNextRetryNotDue_SkipsRecord()
    {
        await using var db = CreateDbContext();
        var now = DateTimeOffset.UtcNow;

        // Upload is queued but NextRetryAt is far in the future
        db.BackupOffsiteUploads.Add(new BackupOffsiteUpload
        {
            Id = Guid.NewGuid(),
            Provider = "google_drive",
            Status = "queued",
            AttemptCount = 1,
            MaxAttempts = 5,
            NextRetryAt = now.AddHours(1), // not due yet
            CreatedAt = now,
            QueuedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("HTTP should not be called when retry is not due.")));

        var service = CreateService(db, httpClient);
        var processed = await service.ProcessNextPendingUploadAsync(CancellationToken.None);

        // Should return false (no upload processed)
        Assert.False(processed);
    }

    [Fact]
    public async Task ProcessNextPendingUploadAsync_WhenResumableSessionExists_SkipsInitiateAndResumes()
    {
        await using var db = CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var backupFilePath = Path.Combine(Path.GetTempPath(), $"congno-drive-resume-{Guid.NewGuid():N}.bak");
        await File.WriteAllTextAsync(backupFilePath, "backup-payload", CancellationToken.None);

        try
        {
            var dataProtectionProvider = new EphemeralDataProtectionProvider();
            var protector = dataProtectionProvider.CreateProtector("CongNoGolden.Backups.GoogleDrive.RefreshToken");

            db.BackupSettings.Add(new BackupSettings
            {
                Id = Guid.NewGuid(), Enabled = true, BackupPath = Path.GetTempPath(),
                RetentionCount = 7, ScheduleDayOfWeek = 1, ScheduleTime = "02:00",
                Timezone = "UTC", PgBinPath = "/usr/bin",
                CreatedAt = now, UpdatedAt = now
            });
            db.BackupOffsiteConnections.Add(new BackupOffsiteConnection
            {
                Id = Guid.NewGuid(), Provider = "google_drive", Status = "connected",
                EncryptedRefreshToken = protector.Protect("refresh-token"),
                ConnectedAt = now, LastValidatedAt = now, CreatedAt = now, UpdatedAt = now
            });
            var job = new BackupJob
            {
                Id = Guid.NewGuid(), Type = "manual", Status = "success",
                FileName = "resume-test.bak", FilePath = backupFilePath, CreatedAt = now
            };
            // Pre-existing session URI means we should NOT call the initiate endpoint
            var upload = new BackupOffsiteUpload
            {
                Id = Guid.NewGuid(), BackupJobId = job.Id, Provider = "google_drive",
                Status = "queued", AttemptCount = 1, MaxAttempts = 5,
                ResumableSessionUri = "https://upload.googleapis.com/session/existing-session",
                CreatedAt = now, QueuedAt = now, UpdatedAt = now
            };
            db.BackupJobs.Add(job);
            db.BackupOffsiteUploads.Add(upload);
            await db.SaveChangesAsync();

            var initiateWasCalled = false;
            using var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
            {
                if (request.RequestUri?.ToString() == "https://oauth2.googleapis.com/token")
                    return Task.FromResult(CreateJsonResponse("""{ "access_token": "access-xyz" }"""));

                // Initiate (POST resumable) must NOT be called
                if (request.Method == HttpMethod.Post &&
                    request.RequestUri?.ToString()?.Contains("uploadType=resumable") == true)
                {
                    initiateWasCalled = true;
                    throw new InvalidOperationException("Initiate should NOT be called when session URI already exists.");
                }

                // Resume query → 308, no bytes received yet
                if (request.Method == HttpMethod.Put &&
                    request.RequestUri?.ToString() == "https://upload.googleapis.com/session/existing-session")
                {
                    // Check if it's the status query (Content-Length 0)
                    if (request.Content?.Headers.ContentLength == 0)
                    {
                        return Task.FromResult(new HttpResponseMessage((HttpStatusCode)308));
                    }

                    // Actual chunk upload: respond with success JSON
                    return Task.FromResult(CreateJsonResponse("""
                        {
                          "id": "file-resumed",
                          "size": "14",
                          "md5Checksum": "resumed123"
                        }
                        """));
                }

                throw new InvalidOperationException($"Unexpected: {request.Method} {request.RequestUri}");
            }));

            var service = CreateService(db, httpClient, dataProtectionProvider: dataProtectionProvider);
            var processed = await service.ProcessNextPendingUploadAsync(CancellationToken.None);

            Assert.True(processed);
            Assert.False(initiateWasCalled, "Initiate endpoint should not be called when session URI already exists.");

            var refreshedUpload = await db.BackupOffsiteUploads.SingleAsync(x => x.Id == upload.Id);
            Assert.Equal("success", refreshedUpload.Status);
            Assert.Equal("file-resumed", refreshedUpload.RemoteFileId);
            Assert.Null(refreshedUpload.ResumableSessionUri); // cleared on success
        }
        finally
        {
            if (File.Exists(backupFilePath)) File.Delete(backupFilePath);
        }
    }

    private static ConGNoDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ConGNoDbContext>()
            .UseInMemoryDatabase($"google_drive_offsite_{Guid.NewGuid():N}")
            .Options;

        return new ConGNoDbContext(options);
    }

    private static GoogleDriveBackupOffsiteService CreateService(
        ConGNoDbContext db,
        HttpClient httpClient,
        GoogleDriveBackupOffsiteOptions? options = null,
        IDataProtectionProvider? dataProtectionProvider = null)
    {
        return new GoogleDriveBackupOffsiteService(
            db,
            httpClient,
            dataProtectionProvider ?? new EphemeralDataProtectionProvider(),
            Options.Create(options ?? CreateOptions()),
            NullLogger<GoogleDriveBackupOffsiteService>.Instance);
    }

    private static GoogleDriveBackupOffsiteOptions CreateOptions()
    {
        return new GoogleDriveBackupOffsiteOptions
        {
            Enabled = true,
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
            TokenEndpoint = "https://oauth2.googleapis.com/token",
            UploadEndpoint = "https://www.googleapis.com/upload/drive/v3/files",
            Scope = "https://www.googleapis.com/auth/drive.file"
        };
    }

    private static HttpResponseMessage CreateJsonResponse(string payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
