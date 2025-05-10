using System.Net.Http.Json;
using ApiCurrency.Models;
using ApiCurrency.Settings;
using Microsoft.Extensions.Options;

namespace ApiCurrency.ExchangeRateProviders;

public class FrankfurterExchangeRateProvider : IExchangeRateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ExchangeRateSettings _settings;
    public string Name => "Frankfurter";

    public FrankfurterExchangeRateProvider(HttpClient httpClient, IOptions<ExchangeRateSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
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

    public async Task<Dictionary<DateTime, decimal>> GetRatesForPeriod(string fromCurrency, string toCurrency, DateTime start, DateTime end, TimeSpan step)
    {
        var url = $"https://api.frankfurter.app/{start:yyyy-MM-dd}..?from={fromCurrency}&to={toCurrency}&start_date={start:yyyy-MM-dd}&end_date={end:yyyy-MM-dd}";
        var response = await _httpClient.GetFromJsonAsync<FrankfurterHistoricalResponse>(url);
        if (response?.Rates == null)
        {
            throw new Exception($"Failed to get exchange rates for {fromCurrency}/{toCurrency}");
        }

        var allRates = response.Rates.ToDictionary(
            kvp => DateTime.Parse(kvp.Key),
            kvp => kvp.Value[toCurrency]);

        var result = new Dictionary<DateTime, decimal>();
        var currentDate = start;
        while (currentDate <= end)
        {
            
            var nearestDate = allRates.Keys
                .Where(d => d >= currentDate)
                .OrderBy(d => d)
                .FirstOrDefault();

            if (nearestDate != default)
            {
                result[nearestDate] = allRates[nearestDate];
            }

            currentDate = currentDate.Add(step);
        }

        return result;
    }

    private bool ShouldUseLatest(DateTime date)
    {
        return (DateTime.UtcNow - date).TotalMinutes <= _settings.FreshnessWindowMinutes;
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