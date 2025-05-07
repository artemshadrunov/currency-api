namespace ApiCurrency.Models;

public class CurrencyConversionRequest
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public decimal Amount { get; set; }
} 