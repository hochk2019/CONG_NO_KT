namespace CongNoGolden.Infrastructure.Services.Common;

public sealed class ReadModelCacheOptions
{
    public bool Enabled { get; set; } = true;
    public int NamespaceVersionHours { get; set; } = 24;
}
