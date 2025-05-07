using ApiCurrency.Models;

namespace ApiCurrency.Services;

public class CurrencyConverterServiceMock : ICurrencyConverterService
{
    private const decimal MockRate = 1.1m;

    public Task<CurrencyConversionResult> ConvertAsync(CurrencyConversionRequest request)
    {
        var result = new CurrencyConversionResult
        {
            From = request.From,
            To = request.To,
            Amount = request.Amount,
            Rate = MockRate,
            Result = request.Amount * MockRate
        };

        return Task.FromResult(result);
    }
} 