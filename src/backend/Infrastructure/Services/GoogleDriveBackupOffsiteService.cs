using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CongNoGolden.Application.Backups;
using CongNoGolden.Application.Common;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Infrastructure.Services;

public sealed class GoogleDriveBackupOffsiteOptions
{
    public const string SectionName = "BackupOffsiteGoogleDrive";

    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AuthorizationEndpoint { get; set; } = "https://accounts.google.com/o/oauth2/v2/auth";
    public string TokenEndpoint { get; set; } = "https://oauth2.googleapis.com/token";
    public string UploadEndpoint { get; set; } = "https://www.googleapis.com/upload/drive/v3/files";
    public string Scope { get; set; } = "https://www.googleapis.com/auth/drive.file";

    public bool IsConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !string.IsNullOrWhiteSpace(AuthorizationEndpoint) &&
        !string.IsNullOrWhiteSpace(TokenEndpoint) &&
        !string.IsNullOrWhiteSpace(UploadEndpoint) &&
        !string.IsNullOrWhiteSpace(Scope);
}

public sealed class GoogleDriveBackupOffsiteService : IBackupOffsiteService
{
    private const string ProviderName = "google_drive";
    private const string ConnectedStatus = "connected";
    private const string DisconnectedStatus = "disconnected";
    private const string ErrorStatus = "error";
    private const string UploadQueuedStatus = "queued";
    private const string UploadProcessingStatus = "processing";
    private const string UploadSuccessStatus = "success";
    private const string UploadFailedStatus = "failed";
    private const string DataProtectionPurpose = "CongNoGolden.Backups.GoogleDrive.RefreshToken";
    private const int MaxPageSize = 200;

    private readonly ConGNoDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly IDataProtector _protector;
    private readonly GoogleDriveBackupOffsiteOptions _options;
    private readonly ILogger<GoogleDriveBackupOffsiteService> _logger;

    public GoogleDriveBackupOffsiteService(
        ConGNoDbContext dbContext,
        HttpClient httpClient,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<GoogleDriveBackupOffsiteOptions> options,
        ILogger<GoogleDriveBackupOffsiteService> logger)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
        _options = options.Value;
        _logger = logger;
    }

    public Task<string> BuildGoogleDriveConnectUrlAsync(string redirectUri, string? folderId, CancellationToken ct)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            throw new InvalidOperationException("Google Drive redirect URI is required.");
        }

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = _options.Scope,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["include_granted_scopes"] = "true"
        };

        return Task.FromResult(BuildUrl(_options.AuthorizationEndpoint, query));
    }

    public async Task<BackupOffsiteConnectionStatus> CompleteGoogleDriveCallbackAsync(
        string code,
        string redirectUri,
        string? folderId,
        CancellationToken ct)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Google Drive authorization code is required.");
        }

        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            throw new InvalidOperationException("Google Drive redirect URI is required.");
        }

        var tokenResponse = await ExchangeAuthorizationCodeAsync(code, redirectUri, ct);
        if (string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
        {
            throw new InvalidOperationException("Google Drive did not return a refresh token. Reconnect and try again.");
        }

        var now = DateTimeOffset.UtcNow;
        var connection = await GetConnectionEntityAsync(track: true, ct) ?? new BackupOffsiteConnection
        {
            Id = Guid.NewGuid(),
            Provider = ProviderName,
            CreatedAt = now
        };

        connection.Provider = ProviderName;
        connection.Status = ConnectedStatus;
        connection.EncryptedRefreshToken = _protector.Protect(tokenResponse.RefreshToken);
        connection.GoogleDriveFolderId = NormalizeOptional(folderId);
        connection.ConnectedAt ??= now;
        connection.LastValidatedAt = now;
        connection.LastError = null;
        connection.UpdatedAt = now;

        if (_dbContext.Entry(connection).State == EntityState.Detached)
        {
            _dbContext.BackupOffsiteConnections.Add(connection);
        }

        await _dbContext.SaveChangesAsync(ct);
        return MapConnectionStatus(connection);
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        var connection = await GetConnectionEntityAsync(track: true, ct);
        if (connection is null)
        {
            return;
        }

        connection.Status = DisconnectedStatus;
        connection.EncryptedRefreshToken = null;
        connection.LastError = null;
        connection.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task EnqueueUploadForBackupJobAsync(Guid backupJobId, CancellationToken ct)
    {
        if (!_options.IsConfigured || backupJobId == Guid.Empty)
        {
            return;
        }

        var exists = await _dbContext.BackupOffsiteUploads.AnyAsync(
            x => x.BackupJobId == backupJobId &&
                 x.Provider == ProviderName &&
                 (x.Status == UploadQueuedStatus || x.Status == UploadProcessingStatus || x.Status == UploadSuccessStatus),
            ct);

        if (exists)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        _dbContext.BackupOffsiteUploads.Add(new BackupOffsiteUpload
        {
            Id = Guid.NewGuid(),
            BackupJobId = backupJobId,
            Provider = ProviderName,
            Status = UploadQueuedStatus,
            AttemptCount = 0,
            CreatedAt = now,
            QueuedAt = now,
            UpdatedAt = now
        });

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<BackupOffsiteConnectionStatus> GetConnectionStatusAsync(CancellationToken ct)
    {
        var connection = await GetConnectionEntityAsync(track: false, ct);
        return connection is null
            ? new BackupOffsiteConnectionStatus(false, ProviderName, null, null, null, null)
            : MapConnectionStatus(connection);
    }

    public async Task<PagedResult<BackupOffsiteUploadDto>> ListUploadsAsync(int page, int pageSize, CancellationToken ct)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _dbContext.BackupOffsiteUploads
            .AsNoTracking()
            .Where(x => x.Provider == ProviderName)
            .OrderByDescending(x => x.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new BackupOffsiteUploadDto(
                x.Id,
                x.BackupJobId,
                x.Provider,
                x.Status,
                x.RemoteFileId,
                x.RemoteChecksum,
                x.RemoteFileSize,
                x.AttemptCount,
                x.ErrorMessage,
                x.CreatedAt,
                x.QueuedAt,
                x.CompletedAt))
            .ToListAsync(ct);

        return new PagedResult<BackupOffsiteUploadDto>(items, safePage, safePageSize, total);
    }

    public async Task<bool> ProcessNextPendingUploadAsync(CancellationToken ct)
    {
        if (!_options.IsConfigured)
        {
            return false;
        }

        var upload = await _dbContext.BackupOffsiteUploads
            .Where(x => x.Provider == ProviderName && x.Status == UploadQueuedStatus)
            .OrderBy(x => x.QueuedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (upload is null)
        {
            return false;
        }

        upload.Status = UploadProcessingStatus;
        upload.AttemptCount += 1;
        upload.ErrorMessage = null;
        upload.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        try
        {
            var backupJob = await ResolveBackupJobAsync(upload, ct);
            var filePath = backupJob.FilePath;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new InvalidOperationException($"Backup file was not found for offsite upload: {filePath ?? "<null>"}");
            }

            var connection = await RequireConnectedConnectionAsync(ct);
            var tokenResponse = await RefreshAccessTokenAsync(connection, ct);
            if (!string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
            {
                connection.EncryptedRefreshToken = _protector.Protect(tokenResponse.RefreshToken);
            }

            var targetFolderId = await ResolveFolderIdAsync(connection, ct);
            await using var fileStream = File.OpenRead(filePath);
            var fileName = string.IsNullOrWhiteSpace(backupJob.FileName) ? Path.GetFileName(filePath) : backupJob.FileName;
            var uploadResult = await UploadFileAsync(
                tokenResponse.AccessToken,
                fileName,
                fileStream,
                fileStream.Length,
                targetFolderId,
                ct);

            upload.Status = UploadSuccessStatus;
            upload.RemoteFileId = uploadResult.FileId;
            upload.RemoteChecksum = uploadResult.Md5Checksum;
            upload.RemoteFileSize = uploadResult.Size ?? fileStream.Length;
            upload.ErrorMessage = null;
            upload.CompletedAt = DateTimeOffset.UtcNow;
            upload.UpdatedAt = upload.CompletedAt.Value;

            connection.Status = ConnectedStatus;
            connection.LastValidatedAt = upload.CompletedAt;
            connection.LastError = null;
            connection.UpdatedAt = upload.CompletedAt.Value;

            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Drive offsite upload failed for upload {UploadId}", upload.Id);
            await MarkUploadFailedAsync(upload, ex.Message, ct);
        }

        return true;
    }

    public async Task<BackupOffsiteUploadDto> ReuploadAsync(Guid backupJobId, CancellationToken ct)
    {
        EnsureConfigured();
        await EnsureConnectedAsync(ct);

        var backupJob = await _dbContext.BackupJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == backupJobId, ct);

        if (backupJob is null)
        {
            throw new InvalidOperationException("Backup job was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var upload = new BackupOffsiteUpload
        {
            Id = Guid.NewGuid(),
            BackupJobId = backupJobId,
            Provider = ProviderName,
            Status = UploadQueuedStatus,
            AttemptCount = 0,
            CreatedAt = now,
            QueuedAt = now,
            UpdatedAt = now
        };

        _dbContext.BackupOffsiteUploads.Add(upload);
        await _dbContext.SaveChangesAsync(ct);
        return MapUploadDto(upload);
    }

    public async Task<BackupOffsiteUploadDto> TestUploadAsync(CancellationToken ct)
    {
        EnsureConfigured();
        var connection = await RequireConnectedConnectionAsync(ct);
        var tokenResponse = await RefreshAccessTokenAsync(connection, ct);
        if (!string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
        {
            connection.EncryptedRefreshToken = _protector.Protect(tokenResponse.RefreshToken);
        }

        var now = DateTimeOffset.UtcNow;
        var contents = Encoding.UTF8.GetBytes($"congno-offsite-test {now:O}");
        await using var stream = new MemoryStream(contents, writable: false);
        var targetFolderId = await ResolveFolderIdAsync(connection, ct);
        var uploadResult = await UploadFileAsync(
            tokenResponse.AccessToken,
            $"congno-offsite-test-{now:yyyyMMddHHmmss}.txt",
            stream,
            contents.Length,
            targetFolderId,
            ct);

        connection.Status = ConnectedStatus;
        connection.LastValidatedAt = now;
        connection.LastError = null;
        connection.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(ct);

        return new BackupOffsiteUploadDto(
            Guid.NewGuid(),
            null,
            ProviderName,
            UploadSuccessStatus,
            uploadResult.FileId,
            uploadResult.Md5Checksum,
            uploadResult.Size ?? contents.Length,
            0,
            null,
            now,
            now,
            now);
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw CreateNotConfiguredException();
        }
    }

    private static InvalidOperationException CreateNotConfiguredException() =>
        new("Offsite backup is not configured.");

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static FormUrlEncodedContent CreateFormContent(IReadOnlyDictionary<string, string?> values)
    {
        var payload = values
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => new KeyValuePair<string, string>(x.Key, x.Value!));

        return new FormUrlEncodedContent(payload);
    }

    private static string BuildUrl(string baseUrl, IReadOnlyDictionary<string, string?> query)
    {
        var queryString = string.Join(
            "&",
            query
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!)}"));

        return string.IsNullOrEmpty(queryString) ? baseUrl : $"{baseUrl}?{queryString}";
    }

    private async Task<BackupOffsiteConnection?> GetConnectionEntityAsync(bool track, CancellationToken ct)
    {
        var query = _dbContext.BackupOffsiteConnections.Where(x => x.Provider == ProviderName);
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync(ct);
    }

    private BackupOffsiteConnectionStatus MapConnectionStatus(BackupOffsiteConnection connection)
    {
        var isConnected =
            connection.Status == ConnectedStatus &&
            !string.IsNullOrWhiteSpace(connection.EncryptedRefreshToken);

        return new BackupOffsiteConnectionStatus(
            isConnected,
            connection.Provider,
            connection.GoogleDriveFolderId,
            connection.ConnectedAt,
            connection.LastValidatedAt,
            connection.LastError);
    }

    private static BackupOffsiteUploadDto MapUploadDto(BackupOffsiteUpload upload) =>
        new(
            upload.Id,
            upload.BackupJobId,
            upload.Provider,
            upload.Status,
            upload.RemoteFileId,
            upload.RemoteChecksum,
            upload.RemoteFileSize,
            upload.AttemptCount,
            upload.ErrorMessage,
            upload.CreatedAt,
            upload.QueuedAt,
            upload.CompletedAt);

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        await RequireConnectedConnectionAsync(ct);
    }

    private async Task<BackupOffsiteConnection> RequireConnectedConnectionAsync(CancellationToken ct)
    {
        var connection = await GetConnectionEntityAsync(track: true, ct);
        if (connection is null || string.IsNullOrWhiteSpace(connection.EncryptedRefreshToken))
        {
            throw new InvalidOperationException("Google Drive is not connected.");
        }

        return connection;
    }

    private async Task<BackupJob> ResolveBackupJobAsync(BackupOffsiteUpload upload, CancellationToken ct)
    {
        if (upload.BackupJobId is null)
        {
            throw new InvalidOperationException("Offsite upload is missing the backup job reference.");
        }

        var backupJob = await _dbContext.BackupJobs.SingleOrDefaultAsync(x => x.Id == upload.BackupJobId.Value, ct);
        return backupJob ?? throw new InvalidOperationException("Backup job was not found for offsite upload.");
    }

    private async Task<string?> ResolveFolderIdAsync(BackupOffsiteConnection connection, CancellationToken ct)
    {
        var settingsFolderId = await _dbContext.BackupSettings
            .AsNoTracking()
            .Select(x => x.GoogleDriveFolderId)
            .FirstOrDefaultAsync(ct);

        return NormalizeOptional(settingsFolderId) ?? NormalizeOptional(connection.GoogleDriveFolderId);
    }

    private async Task<TokenResult> ExchangeAuthorizationCodeAsync(string code, string redirectUri, CancellationToken ct)
    {
        var request = new Dictionary<string, string?>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        };

        using var response = await _httpClient.PostAsync(_options.TokenEndpoint, CreateFormContent(request), ct);
        return await ParseTokenResponseAsync(response, ct);
    }

    private async Task<TokenResult> RefreshAccessTokenAsync(BackupOffsiteConnection connection, CancellationToken ct)
    {
        try
        {
            var refreshToken = _protector.Unprotect(connection.EncryptedRefreshToken!);
            var request = new Dictionary<string, string?>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            };

        using var response = await _httpClient.PostAsync(_options.TokenEndpoint, CreateFormContent(request), ct);
            return await ParseTokenResponseAsync(response, ct);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            connection.Status = ErrorStatus;
            connection.LastError = $"Google Drive token refresh failed: {ex.Message}";
            connection.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            throw;
        }
    }

    private async Task<TokenResult> ParseTokenResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Google Drive token exchange failed: {ExtractErrorMessage(payload)}");
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var accessToken = root.TryGetProperty("access_token", out var accessTokenElement)
            ? accessTokenElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Google Drive token exchange failed: missing access token.");
        }

        var refreshToken = root.TryGetProperty("refresh_token", out var refreshTokenElement)
            ? refreshTokenElement.GetString()
            : null;

        return new TokenResult(accessToken, refreshToken);
    }

    private async Task<UploadResult> UploadFileAsync(
        string accessToken,
        string fileName,
        Stream fileStream,
        long fileSize,
        string? folderId,
        CancellationToken ct)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["name"] = fileName
        };

        if (!string.IsNullOrWhiteSpace(folderId))
        {
            metadata["parents"] = new[] { folderId };
        }

        using var metadataContent = new StringContent(
            JsonSerializer.Serialize(metadata),
            Encoding.UTF8,
            "application/json");

        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var multipart = new MultipartContent("related");
        multipart.Add(metadataContent);
        multipart.Add(fileContent);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUrl(
                _options.UploadEndpoint,
                new Dictionary<string, string?>
                {
                    ["uploadType"] = "multipart",
                    ["supportsAllDrives"] = "true",
                    ["fields"] = "id,name,size,md5Checksum"
                }));

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = multipart;

        using var response = await _httpClient.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Google Drive upload failed: {ExtractErrorMessage(payload)}");
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var fileId = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(fileId))
        {
            throw new InvalidOperationException("Google Drive upload failed: missing remote file id.");
        }

        long? remoteSize = null;
        if (root.TryGetProperty("size", out var sizeElement))
        {
            remoteSize = sizeElement.ValueKind switch
            {
                JsonValueKind.Number when sizeElement.TryGetInt64(out var numericSize) => numericSize,
                JsonValueKind.String when long.TryParse(sizeElement.GetString(), out var parsedSize) => parsedSize,
                _ => null
            };
        }

        var checksum = root.TryGetProperty("md5Checksum", out var checksumElement)
            ? checksumElement.GetString()
            : null;

        return new UploadResult(fileId, remoteSize, checksum);
    }

    private async Task MarkUploadFailedAsync(BackupOffsiteUpload upload, string errorMessage, CancellationToken ct)
    {
        upload.Status = UploadFailedStatus;
        upload.ErrorMessage = errorMessage;
        upload.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
    }

    private static string ExtractErrorMessage(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return "empty response";
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.TryGetProperty("error_description", out var descriptionElement))
            {
                return descriptionElement.GetString() ?? payload;
            }

            if (root.TryGetProperty("error", out var errorElement))
            {
                if (errorElement.ValueKind == JsonValueKind.String)
                {
                    return errorElement.GetString() ?? payload;
                }

                if (errorElement.ValueKind == JsonValueKind.Object &&
                    errorElement.TryGetProperty("message", out var messageElement))
                {
                    return messageElement.GetString() ?? payload;
                }
            }
        }
        catch (JsonException)
        {
        }

        return payload;
    }

    private sealed record TokenResult(string AccessToken, string? RefreshToken);

    private sealed record UploadResult(string FileId, long? Size, string? Md5Checksum);
}
