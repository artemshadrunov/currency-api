using System.Text.Json.Serialization;

namespace CurrencyConverter.Core.Models;

public class CurrencyConversionResult
{
    [JsonPropertyName("fromCurrency")]
    public string FromCurrency { get; set; } = string.Empty;

    [JsonPropertyName("toCurrency")]
    public string ToCurrency { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("convertedAmount")]
    public decimal ConvertedAmount { get; set; }

    [JsonPropertyName("rate")]
    public decimal Rate { get; set; }

    [JsonPropertyName("conversionTimestamp")]
    public DateTime ConversionTimestamp { get; set; }

    [JsonPropertyName("providerName")]
    public string ProviderName { get; set; } = string.Empty;
}