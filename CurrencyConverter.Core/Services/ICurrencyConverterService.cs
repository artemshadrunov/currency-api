using CurrencyConverter.Core.Models;

namespace CurrencyConverter.Core.Services;

public interface ICurrencyConverterService
{
    Task<CurrencyConversionResult> Convert(CurrencyConversionRequest request);
    Task<Dictionary<string, decimal>> GetLatestRates(LatestRatesRequest request);
    Task<PagedRatesResult> GetHistoricalRates(HistoricalRatesRequest request);
}