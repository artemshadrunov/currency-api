namespace ApiCurrency.Models;

public class CurrencyConversionResult
{
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal ConvertedAmount { get; set; }
    public decimal Rate { get; set; }
    public DateTime ConversionTimestamp { get; set; }
    public string ProviderName { get; set; } = string.Empty;
}