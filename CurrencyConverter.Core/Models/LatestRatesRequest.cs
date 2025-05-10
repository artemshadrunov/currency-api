namespace ApiCurrency.Models;

public class LatestRatesRequest
{
    public string BaseCurrency { get; set; } = string.Empty;
    public List<string> TargetCurrencies { get; set; } = new();
    public string ProviderName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}