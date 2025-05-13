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
        DateTime end,
        TimeSpan step)
    {
        // TODO: Implement caching for historical data
        return await _provider.GetRatesForPeriod(fromCurrency, toCurrency, start, end, step);
    }
}

public static class DateTimeExtensions
{
    public static DateTime RoundDown(this DateTime dateTime, TimeSpan interval)
    {
        var delta = dateTime.Ticks % interval.Ticks;
        return new DateTime(dateTime.Ticks - delta, dateTime.Kind);
    }
}