using ApiCurrency.ExchangeRateProviders;
using CurrencyConverter.Core.Infrastructure.Cache;
using CurrencyConverter.Core.Settings;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Core.ExchangeRateProviders;

public class CachedExchangeRateProvider : IExchangeRateProvider
{
    private readonly IExchangeRateProvider _provider;
    private readonly ICacheProvider _cache;
    private readonly RedisSettings _settings;
    private readonly ILogger<CachedExchangeRateProvider> _logger;
    private const string CacheKeyPrefix = "exchange_rate_";

    public string Name => $"{_provider.Name.ToLower()}cached";

    public CachedExchangeRateProvider(
        IExchangeRateProvider provider,
        ICacheProvider cache,
        IOptions<RedisSettings> settings,
        ILogger<CachedExchangeRateProvider> logger)
    {
        _provider = provider;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<decimal> GetRate(string fromCurrency, string toCurrency, DateTime timestamp)
    {
        var cacheKey = $"{CacheKeyPrefix}{fromCurrency}_{toCurrency}_{timestamp:yyyy-MM-dd}";
        _logger.LogDebug("Attempting to get rate from cache for key: {CacheKey}", cacheKey);

        try
        {
            var cachedRate = await _cache.Get<decimal>(cacheKey);
            if (cachedRate != default)
            {
                _logger.LogDebug("Cache hit for {FromCurrency} to {ToCurrency} on {Date}",
                    fromCurrency, toCurrency, timestamp.Date);
                return cachedRate;
            }

            _logger.LogDebug("Cache miss for {FromCurrency} to {ToCurrency} on {Date}, fetching from provider",
                fromCurrency, toCurrency, timestamp.Date);
            var rate = await _provider.GetRate(fromCurrency, toCurrency, timestamp);

            _logger.LogDebug("Caching rate for {FromCurrency} to {ToCurrency} on {Date}",
                fromCurrency, toCurrency, timestamp.Date);
            await _cache.Set(cacheKey, rate, TimeSpan.FromDays(_settings.DefaultExpirationDays));

            return rate;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache operation failed for {FromCurrency} to {ToCurrency} on {Date}, falling back to provider",
                fromCurrency, toCurrency, timestamp.Date);
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
        _logger.LogDebug("Getting rates for period {FromCurrency} to {ToCurrency} from {StartDate} to {EndDate}",
            fromCurrency, toCurrency, start.Date, end.Date);

        var result = new Dictionary<DateTime, decimal>();
        var missingDates = new List<DateTime>();

        // Check cache for each date first
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            var cacheKey = $"{CacheKeyPrefix}{fromCurrency}_{toCurrency}_{date:yyyy-MM-dd}";
            var cachedRate = await _cache.Get<decimal>(cacheKey);
            if (cachedRate != default)
            {
                _logger.LogDebug("Cache hit for {FromCurrency} to {ToCurrency} on {Date}",
                    fromCurrency, toCurrency, date);
                result[date] = cachedRate;
            }
            else
            {
                _logger.LogDebug("Cache miss for {FromCurrency} to {ToCurrency} on {Date}",
                    fromCurrency, toCurrency, date);
                missingDates.Add(date);
            }
        }

        if (missingDates.Count > 0)
        {
            _logger.LogInformation("Fetching {Count} missing dates for {FromCurrency} to {ToCurrency}",
                missingDates.Count, fromCurrency, toCurrency);

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
                _logger.LogDebug("Fetching rates for segment {StartDate} to {EndDate}",
                    segment.Start.Date, segment.End.Date);
                var fetched = await _provider.GetRatesForPeriod(fromCurrency, toCurrency, segment.Start, segment.End);
                foreach (var kvp in fetched)
                {
                    var cacheKey = $"{CacheKeyPrefix}{fromCurrency}_{toCurrency}_{kvp.Key:yyyy-MM-dd}";
                    _logger.LogDebug("Caching rate for {FromCurrency} to {ToCurrency} on {Date}",
                        fromCurrency, toCurrency, kvp.Key.Date);
                    await _cache.Set(cacheKey, kvp.Value, TimeSpan.FromDays(_settings.CacheRetentionDays));
                    result[kvp.Key] = kvp.Value;
                }
            }
        }

        _logger.LogDebug("Successfully retrieved rates for period {FromCurrency} to {ToCurrency}",
            fromCurrency, toCurrency);
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