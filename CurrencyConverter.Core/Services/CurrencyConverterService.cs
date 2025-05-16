using CurrencyConverter.Core.Models;
using CurrencyConverter.Core.ExchangeRateProviders;
using CurrencyConverter.Core.Settings;

namespace CurrencyConverter.Core.Services;

public class CurrencyConverterService : ICurrencyConverterService
{
    private readonly IExchangeRateProviderFactory _providerFactory;
    private readonly ICurrencyRulesProvider _currencyRulesProvider;
    private const int MaxHistoricalYears = 1;

    public CurrencyConverterService(
        IExchangeRateProviderFactory providerFactory,
        ICurrencyRulesProvider currencyRulesProvider)
    {
        _providerFactory = providerFactory;
        _currencyRulesProvider = currencyRulesProvider;
    }

    public async Task<CurrencyConversionResult> Convert(CurrencyConversionRequest request)
    {
        ValidateConversionRequest(request);

        var provider = _providerFactory.GetProvider(request.ProviderName);
        var rate = await provider.GetRate(request.FromCurrency, request.ToCurrency, request.Timestamp);

        return new CurrencyConversionResult
        {
            FromCurrency = request.FromCurrency,
            ToCurrency = request.ToCurrency,
            Amount = request.Amount,
            ConvertedAmount = request.Amount * rate,
            Rate = rate,
            ConversionTimestamp = request.Timestamp,
            ProviderName = request.ProviderName
        };
    }

    public async Task<Dictionary<string, decimal>> GetLatestRates(LatestRatesRequest request)
    {
        ValidateLatestRatesRequest(request);

        var provider = _providerFactory.GetProvider(request.ProviderName);
        var result = new Dictionary<string, decimal>();

        foreach (var target in request.TargetCurrencies)
        {
            if (_currencyRulesProvider.IsCurrencyExcluded(target))
                continue;

            if (string.Equals(target, request.BaseCurrency, StringComparison.OrdinalIgnoreCase))
                continue;

            result[target] = await provider.GetRate(request.BaseCurrency, target, request.Timestamp);
        }

        return result;
    }

    public async Task<PagedRatesResult> GetHistoricalRates(HistoricalRatesRequest request)
    {
        ValidateHistoricalRatesRequest(request);

        var provider = _providerFactory.GetProvider(request.ProviderName);

        var allRates = await provider.GetRatesForPeriod(
            request.BaseCurrency,
            request.TargetCurrency,
            request.Start,
            request.End);

        var ordered = allRates.OrderBy(x => x.Key).ToList();
        var total = ordered.Count;
        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);

        var paged = ordered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToDictionary(x => x.Key, x => x.Value);

        return new PagedRatesResult
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total,
            TotalPages = totalPages,
            HasNextPage = request.Page < totalPages,
            HasPreviousPage = request.Page > 1,
            Rates = paged
        };
    }

    private void ValidateTimestamp(DateTime timestamp, string paramName)
    {
        var now = DateTime.UtcNow;
        if (timestamp > now)
            throw new ArgumentException($"Timestamp cannot be in the future", paramName);

        var minDate = now.AddYears(-MaxHistoricalYears);
        if (timestamp < minDate)
            throw new ArgumentException($"Timestamp cannot be older than {MaxHistoricalYears} year", paramName);
    }

    private void ValidateConversionRequest(CurrencyConversionRequest request)
    {
        if (string.IsNullOrEmpty(request.ProviderName))
            throw new ArgumentException("Provider name is required", nameof(request));

        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than zero", nameof(request));

        ValidateTimestamp(request.Timestamp, nameof(request.Timestamp));

        if (_currencyRulesProvider.IsCurrencyExcluded(request.FromCurrency))
            throw new InvalidOperationException($"Currency {request.FromCurrency} is excluded from conversion");

        if (_currencyRulesProvider.IsCurrencyExcluded(request.ToCurrency))
            throw new InvalidOperationException($"Currency {request.ToCurrency} is excluded from conversion");
    }

    private void ValidateLatestRatesRequest(LatestRatesRequest request)
    {
        if (string.IsNullOrEmpty(request.ProviderName))
            throw new ArgumentException("Provider name is required", nameof(request));

        if (string.IsNullOrEmpty(request.BaseCurrency))
            throw new ArgumentException("Base currency is required", nameof(request));

        if (!request.TargetCurrencies.Any())
            throw new ArgumentException("At least one target currency is required", nameof(request));

        ValidateTimestamp(request.Timestamp, nameof(request.Timestamp));
    }

    private void ValidateHistoricalRatesRequest(HistoricalRatesRequest request)
    {
        if (string.IsNullOrEmpty(request.ProviderName))
            throw new ArgumentException("Provider name is required", nameof(request));

        if (_currencyRulesProvider.IsCurrencyExcluded(request.BaseCurrency))
            throw new InvalidOperationException($"Currency {request.BaseCurrency} is excluded from conversion");

        if (_currencyRulesProvider.IsCurrencyExcluded(request.TargetCurrency))
            throw new InvalidOperationException($"Currency {request.TargetCurrency} is excluded from conversion");

        var now = DateTime.UtcNow;
        var minDate = now.AddYears(-MaxHistoricalYears);

        // Check if both dates are within valid range
        if (request.Start < minDate || request.Start > now)
            throw new ArgumentException($"Start date must be between {minDate:yyyy-MM-dd} and {now:yyyy-MM-dd}", nameof(request.Start));

        if (request.End < minDate || request.End > now)
            throw new ArgumentException($"End date must be between {minDate:yyyy-MM-dd} and {now:yyyy-MM-dd}", nameof(request.End));

        if (request.Start > request.End)
            throw new ArgumentException("Start date cannot be later than end date");
    }
}