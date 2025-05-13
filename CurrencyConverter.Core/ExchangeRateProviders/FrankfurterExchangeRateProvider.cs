using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using ApiCurrency.Models;

namespace ApiCurrency.ExchangeRateProviders;

public class FrankfurterExchangeRateProvider : IExchangeRateProvider
{
    private readonly HttpClient _httpClient;
    public string Name => "Frankfurter";

    public FrankfurterExchangeRateProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<decimal> GetRate(string fromCurrency, string toCurrency, DateTime date)
    {
        var url = ShouldUseLatest(date)
            ? $"https://api.frankfurter.app/latest?from={fromCurrency}&to={toCurrency}"
            : $"https://api.frankfurter.app/{date:yyyy-MM-dd}?from={fromCurrency}&to={toCurrency}";

        var response = await _httpClient.GetFromJsonAsync<FrankfurterResponse>(url);
        if (response?.Rates == null || !response.Rates.TryGetValue(toCurrency, out var rate))
        {
            throw new Exception($"Failed to get exchange rate for {fromCurrency}/{toCurrency}");
        }

        return rate;
    }

    public async Task<Dictionary<DateTime, decimal>> GetRatesForPeriod(string fromCurrency, string toCurrency, DateTime start, DateTime end)
    {
        var url = $"https://api.frankfurter.app/{start:yyyy-MM-dd}..{end:yyyy-MM-dd}?from={fromCurrency}&to={toCurrency}";
        var response = await _httpClient.GetFromJsonAsync<FrankfurterHistoricalResponse>(url);
        if (response?.Rates == null)
            throw new Exception($"Failed to get exchange rates for {fromCurrency}/{toCurrency}");

        var allRates = response.Rates.ToDictionary(
            kvp => DateTime.Parse(kvp.Key),
            kvp => kvp.Value[toCurrency]);

        var result = new Dictionary<DateTime, decimal>();
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            if (allRates.TryGetValue(date, out var rate))
                result[date] = rate;
        }
        return result;
    }

    private bool ShouldUseLatest(DateTime date)
    {
        return date.Date == DateTime.UtcNow.Date;
    }
}

public class FrankfurterResponse
{
    public Dictionary<string, decimal> Rates { get; set; } = new();
}

public class FrankfurterHistoricalResponse
{
    public Dictionary<string, Dictionary<string, decimal>> Rates { get; set; } = new();
}