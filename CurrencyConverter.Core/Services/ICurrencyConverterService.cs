using ApiCurrency.Models;

namespace ApiCurrency.Services;

public interface ICurrencyConverterService
{
    Task<CurrencyConversionResult> ConvertAsync(CurrencyConversionRequest request);
} 