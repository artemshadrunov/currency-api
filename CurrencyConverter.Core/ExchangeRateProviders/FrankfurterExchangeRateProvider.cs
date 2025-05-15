using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using ApiCurrency.Models;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using System.Diagnostics;

namespace ApiCurrency.ExchangeRateProviders;

public class FrankfurterExchangeRateProvider : IExchangeRateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FrankfurterExchangeRateProvider> _logger;
    private const string CorrelationIdHeader = "X-Correlation-ID";
    public string Name => "Frankfurter";

    public FrankfurterExchangeRateProvider(
        HttpClient httpClient,
        ILogger<FrankfurterExchangeRateProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<decimal> GetRate(string fromCurrency, string toCurrency, DateTime date)
    {
        var url = ShouldUseLatest(date)
            ? $"https://api.frankfurter.app/latest?from={fromCurrency}&to={toCurrency}"
            : $"https://api.frankfurter.app/{date:yyyy-MM-dd}?from={fromCurrency}&to={toCurrency}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Add correlation ID from current log context
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
        request.Headers.Add(CorrelationIdHeader, correlationId);

        _logger.LogDebug("Requesting exchange rate from Frankfurter API: {Url}", url);
        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var frankfurterResponse = await response.Content.ReadFromJsonAsync<FrankfurterResponse>();

            if (frankfurterResponse?.Rates == null || !frankfurterResponse.Rates.TryGetValue(toCurrency, out var rate))
            {
                _logger.LogError("Failed to get exchange rate for {FromCurrency}/{ToCurrency} on {Date}",
                    fromCurrency, toCurrency, date);
                throw new Exception($"Failed to get exchange rate for {fromCurrency}/{toCurrency}");
            }

            _logger.LogDebug("Successfully retrieved rate {Rate} for {FromCurrency}/{ToCurrency} on {Date}",
                rate, fromCurrency, toCurrency, date);
            return rate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting exchange rate from Frankfurter API for {FromCurrency}/{ToCurrency} on {Date}",
                fromCurrency, toCurrency, date);
            throw;
        }
    }

    public async Task<Dictionary<DateTime, decimal>> GetRatesForPeriod(string fromCurrency, string toCurrency, DateTime start, DateTime end)
    {
        var url = $"https://api.frankfurter.app/{start:yyyy-MM-dd}..{end:yyyy-MM-dd}?from={fromCurrency}&to={toCurrency}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Add correlation ID from current log context
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
        request.Headers.Add(CorrelationIdHeader, correlationId);

        _logger.LogDebug("Requesting historical rates from Frankfurter API: {Url}", url);

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var frankfurterResponse = await response.Content.ReadFromJsonAsync<FrankfurterHistoricalResponse>();

            if (frankfurterResponse?.Rates == null)
            {
                _logger.LogError("Failed to get exchange rates for {FromCurrency}/{ToCurrency} from {StartDate} to {EndDate}",
                    fromCurrency, toCurrency, start, end);
                throw new Exception($"Failed to get exchange rates for {fromCurrency}/{toCurrency}");
            }

            var allRates = frankfurterResponse.Rates.ToDictionary(
                kvp => DateTime.Parse(kvp.Key),
                kvp => kvp.Value[toCurrency]);

            var result = new Dictionary<DateTime, decimal>();
            var currentDate = start;

            // Add dates until we reach the end date (inclusive)
            while (currentDate <= end)
            {
                // Find the nearest available date that is not greater than currentDate
                var nearestDate = allRates.Keys
                    .Where(d => d <= currentDate)
                    .OrderByDescending(d => d)
                    .FirstOrDefault();

                if (nearestDate != default)
                {
                    result[currentDate] = allRates[nearestDate];
                }
                currentDate = currentDate.AddDays(1);
            }

            _logger.LogDebug("Successfully retrieved {Count} rates for {FromCurrency}/{ToCurrency} from {StartDate} to {EndDate}",
                result.Count, fromCurrency, toCurrency, start, end);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting historical rates from Frankfurter API for {FromCurrency}/{ToCurrency} from {StartDate} to {EndDate}",
                fromCurrency, toCurrency, start, end);
            throw;
        }
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