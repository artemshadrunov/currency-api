namespace ApiCurrency.ExchangeRateProviders;

public interface IExchangeRateProviderFactory
{
    IExchangeRateProvider GetProvider(string providerName);
}