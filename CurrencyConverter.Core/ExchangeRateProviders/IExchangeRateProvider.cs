using CurrencyConverter.Core.Models;

namespace CurrencyConverter.Core.ExchangeRateProviders;

public interface IExchangeRateProvider
{
    string Name { get; }
    Task<decimal> GetRate(string fromCurrency, string toCurrency, DateTime date);
    Task<Dictionary<DateTime, decimal>> GetRatesForPeriod(string fromCurrency, string toCurrency, DateTime start, DateTime end);
}