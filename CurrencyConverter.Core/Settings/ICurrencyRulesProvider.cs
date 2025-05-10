namespace ApiCurrency.Settings;

public interface ICurrencyRulesProvider
{
    bool IsCurrencyExcluded(string currencyCode);
}