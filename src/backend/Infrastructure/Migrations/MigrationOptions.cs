namespace CongNoGolden.Infrastructure.Migrations;

public sealed class MigrationOptions
{
    public bool Enabled { get; set; } = true;
    public string? ScriptsPath { get; set; }
    public string? ConnectionString { get; set; }
}
