using ApiCurrency.Models;

namespace ApiCurrency.Services;

public interface ICurrencyConverterService
{
    Task<CurrencyConversionResult> Convert(CurrencyConversionRequest request);
    Task<Dictionary<string, decimal>> GetLatestRates(LatestRatesRequest request);
    Task<PagedRatesResult> GetHistoricalRates(HistoricalRatesRequest request);
}