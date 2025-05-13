namespace ApiCurrency.Models;

public class HistoricalRatesRequest
{
    public string BaseCurrency { get; set; } = string.Empty;
    public string TargetCurrency { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string ProviderName { get; set; } = string.Empty;
}