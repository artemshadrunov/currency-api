using ApiCurrency.Models;
using Microsoft.Extensions.Options;

namespace ApiCurrency.Settings;

public class CurrencyRulesSettingsProvider : ICurrencyRulesProvider
{
    private readonly CurrencyRulesOptions _options;

    public CurrencyRulesSettingsProvider(IOptions<CurrencyRulesOptions> options)
    {
        _options = options.Value;
    }

    public bool IsCurrencyExcluded(string currencyCode)
    {
        if (string.IsNullOrEmpty(currencyCode))
            return false;

        return _options.ExcludedCurrencies
            .Any(excluded => string.Equals(excluded, currencyCode, StringComparison.OrdinalIgnoreCase));
    }
}