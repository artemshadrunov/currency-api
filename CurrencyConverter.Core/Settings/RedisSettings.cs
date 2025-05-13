namespace CurrencyConverter.Core.Settings;

public class RedisSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string InstanceName { get; set; } = string.Empty;
    public int DefaultExpirationDays { get; set; }
    public int CacheRetentionDays { get; set; }
}