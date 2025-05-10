using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ApiCurrency.Models;

namespace ApiCurrency.ExchangeRateProviders;

public class StubExchangeRateProvider : IExchangeRateProvider
{
    public string Name => "Stub";

    public Task<decimal> GetRate(string fromCurrency, string toCurrency, DateTime date)
    {
        return Task.FromResult(1.5m);
    }

    public Task<Dictionary<DateTime, decimal>> GetRatesForPeriod(string fromCurrency, string toCurrency, DateTime start, DateTime end, TimeSpan step)
    {
        var result = new Dictionary<DateTime, decimal>();
        var current = start;

        while (current <= end)
        {
            result[current] = 1.5m;
            current = current.Add(step);
        }

        return Task.FromResult(result);
    }
}