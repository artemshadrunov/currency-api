using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using CurrencyConverter.Core.Models;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using System.Diagnostics;
using CurrencyConverter.Core.Infrastructure.Http;

namespace CurrencyConverter.Core.ExchangeRateProviders;

public class FrankfurterExchangeRateProvider : IExchangeRateProvider
{
    private readonly IHttpClient _httpClient;
    private readonly ILogger<FrankfurterExchangeRateProvider> _logger;
    private const string CorrelationIdHeader = "X-Correlation-ID";
    public string Name => "Frankfurter";

    public FrankfurterExchangeRateProvider(
        IHttpClient httpClient,
        ILogger<FrankfurterExchangeRateProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<decimal> GetRate(string fromCurrency, string toCurrency, DateTime date)
    {
        // Ensure date is in UTC
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        var url = ShouldUseLatest(utcDate)
            ? $"https://api.frankfurter.app/latest?from={fromCurrency}&to={toCurrency}"
            : $"https://api.frankfurter.app/{utcDate:yyyy-MM-dd}?from={fromCurrency}&to={toCurrency}";

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
                    fromCurrency, toCurrency, utcDate);
                throw new Exception($"Failed to get exchange rate for {fromCurrency}/{toCurrency}");
            }

            _logger.LogDebug("Successfully retrieved rate {Rate} for {FromCurrency}/{ToCurrency} on {Date}",
                rate, fromCurrency, toCurrency, utcDate);
            return rate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting exchange rate from Frankfurter API for {FromCurrency}/{ToCurrency} on {Date}",
                fromCurrency, toCurrency, utcDate);
            throw;
        }
    }

    public async Task<Dictionary<DateTime, decimal>> GetRatesForPeriod(string fromCurrency, string toCurrency, DateTime start, DateTime end)
    {
        // Ensure dates are in UTC
        var utcStart = DateTime.SpecifyKind(start.Date, DateTimeKind.Utc);
        var utcEnd = DateTime.SpecifyKind(end.Date, DateTimeKind.Utc);

        var url = $"https://api.frankfurter.app/{utcStart:yyyy-MM-dd}..{utcEnd:yyyy-MM-dd}?from={fromCurrency}&to={toCurrency}";

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
                    fromCurrency, toCurrency, utcStart, utcEnd);
                throw new Exception($"Failed to get exchange rates for {fromCurrency}/{toCurrency}");
            }

            var allRates = frankfurterResponse.Rates.ToDictionary(
                kvp => DateTime.SpecifyKind(DateTime.Parse(kvp.Key), DateTimeKind.Utc),
                kvp => kvp.Value[toCurrency]);

            if (allRates.Count == 0)
            {
                _logger.LogError("No rates found for {FromCurrency}/{ToCurrency} from {StartDate} to {EndDate}",
                    fromCurrency, toCurrency, utcStart, utcEnd);
                throw new Exception($"No rates found for {fromCurrency}/{toCurrency}");
            }

            var result = new Dictionary<DateTime, decimal>();
            var currentDate = utcStart;

            // Add dates until we reach the end date (inclusive)
            while (currentDate <= utcEnd)
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
                result.Count, fromCurrency, toCurrency, utcStart, utcEnd);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting historical rates from Frankfurter API for {FromCurrency}/{ToCurrency} from {StartDate} to {EndDate}",
                fromCurrency, toCurrency, utcStart, utcEnd);
            throw;
        }
    }

    private bool ShouldUseLatest(DateTime date)
    {
        var utcNow = DateTime.UtcNow;
        return date.Date == utcNow.Date;
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