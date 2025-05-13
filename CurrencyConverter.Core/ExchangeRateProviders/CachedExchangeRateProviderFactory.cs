using ApiCurrency.ExchangeRateProviders;
using CurrencyConverter.Core.Infrastructure.Cache;
using CurrencyConverter.Core.Settings;
using Microsoft.Extensions.Options;

namespace CurrencyConverter.Core.ExchangeRateProviders;

public class CachedExchangeRateProviderFactory : IExchangeRateProviderFactory
{
    private readonly ICacheProvider _cache;
    private readonly RedisSettings _settings;
    private readonly Dictionary<string, IExchangeRateProvider> _providers;

    public CachedExchangeRateProviderFactory(
        ICacheProvider cache,
        IOptions<RedisSettings> settings,
        IEnumerable<IExchangeRateProvider> providers)
    {
        _cache = cache;
        _settings = settings.Value;
        _providers = providers.ToDictionary(p => p.Name.ToLower());
    }

    public IExchangeRateProvider GetProvider(string providerName)
    {
        if (string.IsNullOrEmpty(providerName))
            throw new ArgumentException("Provider name cannot be empty", nameof(providerName));

        var normalizedName = $"{providerName.ToLower()}cached";
        if (!_providers.TryGetValue(normalizedName, out var provider))
            throw new InvalidOperationException($"Provider '{providerName}' not found");

        return provider;
    }

    public IExchangeRateProvider CreateCachedProvider(IExchangeRateProvider provider)
    {
        return new CachedExchangeRateProvider(provider, _cache, Options.Create(_settings));
    }
}