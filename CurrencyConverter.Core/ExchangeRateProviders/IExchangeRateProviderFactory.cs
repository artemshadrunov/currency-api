namespace CurrencyConverter.Core.ExchangeRateProviders;

public interface IExchangeRateProviderFactory
{
    IExchangeRateProvider GetProvider(string providerName);
    IExchangeRateProvider CreateCachedProvider(IExchangeRateProvider provider);
}