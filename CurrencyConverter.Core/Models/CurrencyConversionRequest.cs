namespace CurrencyConverter.Core.Models;

public class CurrencyConversionRequest
{
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
    public string ProviderName { get; set; } = string.Empty;
}