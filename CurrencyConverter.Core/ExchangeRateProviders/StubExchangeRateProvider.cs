using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CurrencyConverter.Core.Models;

namespace CurrencyConverter.Core.ExchangeRateProviders;

public class StubExchangeRateProvider : IExchangeRateProvider
{
    public string Name => "Stub";

    public Task<decimal> GetRate(string fromCurrency, string toCurrency, DateTime date)
    {
        return Task.FromResult(1.5m);
    }

    public Task<Dictionary<DateTime, decimal>> GetRatesForPeriod(string fromCurrency, string toCurrency, DateTime start, DateTime end)
    {
        var result = new Dictionary<DateTime, decimal>();
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            result[date] = 1.0m;
        }
        return Task.FromResult(result);
    }
}