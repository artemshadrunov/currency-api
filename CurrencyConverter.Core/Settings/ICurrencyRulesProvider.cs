namespace CurrencyConverter.Core.Settings;

public interface ICurrencyRulesProvider
{
    bool IsCurrencyExcluded(string currencyCode);
}