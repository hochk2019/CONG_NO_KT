using System.Security.Cryptography;

namespace CongNoGolden.Application.Backups;

public sealed record BackupDownloadToken(string Token, DateTimeOffset ExpiresAt);

public static class BackupDownloadTokenGenerator
{
    public static BackupDownloadToken CreateToken(DateTimeOffset now, TimeSpan ttl)
    {
        var buffer = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToHexString(buffer);
        return new BackupDownloadToken(token, now.Add(ttl));
    }
}
