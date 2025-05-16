namespace CurrencyConverter.Core.Models;

public class LatestRatesRequest
{
    public string BaseCurrency { get; set; } = string.Empty;
    public List<string> TargetCurrencies { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public string ProviderName { get; set; } = string.Empty;
}