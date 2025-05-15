using ApiCurrency.ExchangeRateProviders;
using ApiCurrency.Models;
using ApiCurrency.Services;
using ApiCurrency.Settings;
using CurrencyConverter.Core.Infrastructure.Cache;
using CurrencyConverter.Core.Settings;
using CurrencyConverter.Core.ExchangeRateProviders;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Tests.IntegrationTests;

public class FrankfurterExchangeRateProviderIntegrationTests
{
    private readonly IExchangeRateProvider _provider;
    private readonly ICurrencyConverterService _service;
    private readonly IExchangeRateProviderFactory _providerFactory;
    private readonly ICurrencyRulesProvider _currencyRulesProvider;
    private readonly ICacheProvider _cacheProvider;

    public FrankfurterExchangeRateProviderIntegrationTests()
    {
        var httpClient = new HttpClient();
        var frankLogger = new Mock<ILogger<FrankfurterExchangeRateProvider>>();
        var baseProvider = new FrankfurterExchangeRateProvider(httpClient, frankLogger.Object);

        var mockDistributedCache = new Mock<IDistributedCache>();
        var mockConfiguration = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();

        mockSection.Setup(x => x.Value).Returns("30");
        mockConfiguration.Setup(x => x.GetSection("Redis:DefaultExpirationDays")).Returns(mockSection.Object);

        _cacheProvider = new RedisCacheProvider(
            ConnectionMultiplexer.Connect("localhost:6379"),
            Options.Create(new RedisSettings { CacheRetentionDays = 30 }));

        var cacheLogger = new Mock<ILogger<CachedExchangeRateProvider>>();
        _provider = new CachedExchangeRateProvider(
            baseProvider,
            _cacheProvider,
            Options.Create(new RedisSettings()),
            cacheLogger.Object);

        var factoryLogger = new Mock<ILogger<CachedExchangeRateProvider>>();
        _providerFactory = new CachedExchangeRateProviderFactory(
            _cacheProvider,
            Options.Create(new RedisSettings()),
            new List<IExchangeRateProvider> { _provider },
            factoryLogger.Object);

        _currencyRulesProvider = new CurrencyRulesSettingsProvider(Options.Create(new CurrencyRulesOptions
        {
            ExcludedCurrencies = new List<string> { "TRY" }
        }));

        _service = new CurrencyConverterService(_providerFactory, _currencyRulesProvider);
    }

    [Fact]
    public async Task GetRate_ReturnsValidRate()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "EUR";
        var timestamp = DateTime.UtcNow.Date.AddDays(-1);

        // Act
        var rate = await _provider.GetRate(fromCurrency, toCurrency, timestamp);

        // Assert
        Assert.True(rate > 0);
    }

    [Fact]
    public async Task GetRatesForPeriod_ReturnsValidRates()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "EUR";
        var start = new DateTime(2025, 5, 5);
        var end = new DateTime(2025, 5, 10);

        // Act
        var rates = await _provider.GetRatesForPeriod(fromCurrency, toCurrency, start, end);

        // Assert
        Assert.NotEmpty(rates);
        Assert.All(rates, kvp => Assert.True(kvp.Value > 0));
    }

    [Fact]
    public async Task Convert_ReturnsValidResult()
    {
        // Arrange
        var request = new CurrencyConversionRequest
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Amount = 100,
            Timestamp = DateTime.UtcNow.Date.AddDays(-1),
            ProviderName = "Frankfurter"
        };

        // Act
        var result = await _service.Convert(request);

        // Assert
        Assert.Equal("USD", result.FromCurrency);
        Assert.Equal("EUR", result.ToCurrency);
        Assert.Equal(100, result.Amount);
        Assert.True(result.ConvertedAmount > 0);
        Assert.True(result.Rate > 0);
        Assert.Equal(request.Timestamp.Date, result.ConversionTimestamp.Date);
        Assert.Equal("Frankfurter", result.ProviderName);
    }

    [Fact]
    public async Task GetLatestRates_ReturnsValidRates()
    {
        // Arrange
        var request = new LatestRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrencies = new List<string> { "EUR", "GBP", "JPY" },
            Timestamp = DateTime.UtcNow.Date,
            ProviderName = "Frankfurter"
        };

        // Act
        var rates = await _service.GetLatestRates(request);

        // Assert
        Assert.Equal(3, rates.Count);
        Assert.All(rates, kvp => Assert.True(kvp.Value > 0));
    }

    [Fact]
    public async Task GetHistoricalRates_ReturnsValidPagedResult()
    {
        // Arrange
        var request = new HistoricalRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrency = "EUR",
            Start = new DateTime(2025, 5, 5),
            End = new DateTime(2025, 5, 10),
            Page = 1,
            PageSize = 3,
            ProviderName = "Frankfurter"
        };

        // Act
        var result = await _service.GetHistoricalRates(request);

        // Assert
        Assert.Equal(1, result.Page);
        Assert.Equal(3, result.PageSize);
        Assert.Equal(6, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
        Assert.Equal(3, result.Rates.Count);
        Assert.All(result.Rates, kvp => Assert.True(kvp.Value > 0));
    }

    [Fact]
    public async Task GetHistoricalRates_SecondPage_ReturnsValidPagedResult()
    {
        // Arrange
        var request = new HistoricalRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrency = "EUR",
            Start = new DateTime(2025, 5, 5),
            End = new DateTime(2025, 5, 10),
            Page = 2,
            PageSize = 3,
            ProviderName = "Frankfurter"
        };

        // Act
        var result = await _service.GetHistoricalRates(request);

        // Assert
        Assert.Equal(2, result.Page);
        Assert.Equal(3, result.PageSize);
        Assert.Equal(6, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.False(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
        Assert.Equal(3, result.Rates.Count);
        Assert.All(result.Rates, kvp => Assert.True(kvp.Value > 0));
    }

    [Fact]
    public async Task GetHistoricalRates_LastPage_ReturnsValidPagedResult()
    {
        // Arrange
        var request = new HistoricalRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrency = "EUR",
            Start = new DateTime(2025, 5, 5),
            End = new DateTime(2025, 5, 10),
            Page = 3,
            PageSize = 3,
            ProviderName = "Frankfurter"
        };

        // Act
        var result = await _service.GetHistoricalRates(request);

        // Assert
        Assert.Equal(3, result.Page);
        Assert.Equal(3, result.PageSize);
        Assert.Equal(6, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.False(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
        Assert.Empty(result.Rates);
    }
}