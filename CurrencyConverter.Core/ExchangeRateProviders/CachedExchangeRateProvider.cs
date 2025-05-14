using ApiCurrency.ExchangeRateProviders;
using CurrencyConverter.Core.Infrastructure.Cache;
using CurrencyConverter.Core.Settings;
using Microsoft.Extensions.Options;

namespace CurrencyConverter.Core.ExchangeRateProviders;

public class CachedExchangeRateProvider : IExchangeRateProvider
{
    private readonly IExchangeRateProvider _provider;
    private readonly ICacheProvider _cache;
    private readonly RedisSettings _settings;
    private const string CacheKeyPrefix = "exchange_rate_";

    public string Name => $"{_provider.Name.ToLower()}cached";

    public CachedExchangeRateProvider(
        IExchangeRateProvider provider,
        ICacheProvider cache,
        IOptions<RedisSettings> settings)
    {
        _provider = provider;
        _cache = cache;
        _settings = settings.Value;
    }

    public async Task<decimal> GetRate(string fromCurrency, string toCurrency, DateTime timestamp)
    {
        try
        {
            var cacheKey = $"{CacheKeyPrefix}{fromCurrency}_{toCurrency}_{timestamp:yyyy-MM-dd}";

            var cachedRate = await _cache.Get<decimal>(cacheKey);
            if (cachedRate != default)
            {
                return cachedRate;
            }

            var rate = await _provider.GetRate(fromCurrency, toCurrency, timestamp);
            await _cache.Set(cacheKey, rate, TimeSpan.FromDays(_settings.DefaultExpirationDays));

            return rate;
        }
        catch
        {
            // If cache fails, go directly to API
            return await _provider.GetRate(fromCurrency, toCurrency, timestamp);
        }
    }

    public async Task<Dictionary<DateTime, decimal>> GetRatesForPeriod(
        string fromCurrency,
        string toCurrency,
        DateTime start,
        DateTime end)
    {
        var result = new Dictionary<DateTime, decimal>();
        var missingDates = new List<DateTime>();

        // Check cache for each date first
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            var cacheKey = $"{CacheKeyPrefix}{fromCurrency}_{toCurrency}_{date:yyyy-MM-dd}";
            var cachedRate = await _cache.Get<decimal>(cacheKey);
            if (cachedRate != default)
            {
                result[date] = cachedRate;
            }
            else
            {
                missingDates.Add(date);
            }
        }

        if (missingDates.Count > 0)
        {
            // Group dates into continuous segments
            var segments = new List<(DateTime Start, DateTime End)>();
            var currentSegment = (Start: missingDates[0], End: missingDates[0]);

            for (int i = 1; i < missingDates.Count; i++)
            {
                if ((missingDates[i] - currentSegment.End).TotalDays == 1)
                {
                    currentSegment.End = missingDates[i];
                }
                else
                {
                    segments.Add(currentSegment);
                    currentSegment = (Start: missingDates[i], End: missingDates[i]);
                }
            }
            segments.Add(currentSegment);

            // Request data for each segment
            foreach (var segment in segments)
            {
                var fetched = await _provider.GetRatesForPeriod(fromCurrency, toCurrency, segment.Start, segment.End);
                foreach (var kvp in fetched)
                {
                    var cacheKey = $"{CacheKeyPrefix}{fromCurrency}_{toCurrency}_{kvp.Key:yyyy-MM-dd}";
                    await _cache.Set(cacheKey, kvp.Value, TimeSpan.FromDays(_settings.CacheRetentionDays));
                    result[kvp.Key] = kvp.Value;
                }
            }
        }

        return result;
    }
}

public static class DateTimeExtensions
{
    public static DateTime RoundDown(this DateTime dateTime, TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentException("Interval must be greater than zero", nameof(interval));

        var delta = dateTime.Ticks % interval.Ticks;
        return new DateTime(dateTime.Ticks - delta, dateTime.Kind);
    }
}