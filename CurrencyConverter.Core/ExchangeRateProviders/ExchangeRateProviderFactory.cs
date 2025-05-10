namespace ApiCurrency.ExchangeRateProviders;

public class ExchangeRateProviderFactory : IExchangeRateProviderFactory
{
    private readonly Dictionary<string, IExchangeRateProvider> _providers;

    public ExchangeRateProviderFactory(IEnumerable<IExchangeRateProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Name.ToLower());
    }

    public IExchangeRateProvider GetProvider(string providerName)
    {
        if (string.IsNullOrEmpty(providerName))
            throw new ArgumentException("Provider name is required", nameof(providerName));

        if (!_providers.TryGetValue(providerName.ToLower(), out var provider))
            throw new ArgumentException($"Provider '{providerName}' not found", nameof(providerName));

        return provider;
    }
}