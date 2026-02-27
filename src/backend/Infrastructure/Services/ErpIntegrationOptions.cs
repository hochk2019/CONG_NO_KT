namespace CongNoGolden.Infrastructure.Services;

public sealed class ErpIntegrationOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "MISA";
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string CompanyCode { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 15;
}
