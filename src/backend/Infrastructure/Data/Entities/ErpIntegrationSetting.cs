namespace CongNoGolden.Infrastructure.Data.Entities;

public sealed class ErpIntegrationSetting
{
    public Guid Id { get; set; }
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "MISA";
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? CompanyCode { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
