using ApiCurrency.ExchangeRateProviders;
using ApiCurrency.Models;
using ApiCurrency.Services;
using ApiCurrency.Settings;
using Moq;
using Xunit;

namespace CurrencyConverter.Tests.UnitTests;

public class CurrencyConverterServiceTests
{
    private readonly Mock<IExchangeRateProviderFactory> _mockProviderFactory;
    private readonly Mock<IExchangeRateProvider> _mockProvider;
    private readonly Mock<ICurrencyRulesProvider> _mockCurrencyRulesProvider;
    private readonly CurrencyConverterService _service;

    public CurrencyConverterServiceTests()
    {
        _mockProviderFactory = new Mock<IExchangeRateProviderFactory>();
        _mockProvider = new Mock<IExchangeRateProvider>();
        _mockCurrencyRulesProvider = new Mock<ICurrencyRulesProvider>();
        _service = new CurrencyConverterService(_mockProviderFactory.Object, _mockCurrencyRulesProvider.Object);

        _mockProviderFactory
            .Setup(x => x.GetProvider(It.IsAny<string>()))
            .Returns(_mockProvider.Object);

        _mockCurrencyRulesProvider
            .Setup(x => x.IsCurrencyExcluded(It.IsAny<string>()))
            .Returns(false);
    }

    [Fact]
    public async Task Convert_ReturnsCorrectResult()
    {
        // Arrange
        var timestamp = DateTime.UtcNow.AddDays(-1);
        _mockProvider
            .Setup(x => x.GetRate("USD", "EUR", timestamp))
            .ReturnsAsync(1.5m);

        var request = new CurrencyConversionRequest
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Amount = 100,
            Timestamp = timestamp,
            ProviderName = "Stub"
        };

        // Act
        var result = await _service.Convert(request);

        // Assert
        Assert.Equal("USD", result.FromCurrency);
        Assert.Equal("EUR", result.ToCurrency);
        Assert.Equal(100, result.Amount);
        Assert.Equal(150, result.ConvertedAmount);
        Assert.Equal(1.5m, result.Rate);
        Assert.Equal(timestamp, result.ConversionTimestamp);
        Assert.Equal("Stub", result.ProviderName);
    }

    [Fact]
    public async Task Convert_ThrowsOnZeroAmount()
    {
        // Arrange
        var request = new CurrencyConversionRequest
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Amount = 0,
            Timestamp = DateTime.UtcNow,
            ProviderName = "Stub"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.Convert(request));
    }

    [Fact]
    public async Task Convert_ThrowsOnNegativeAmount()
    {
        // Arrange
        var request = new CurrencyConversionRequest
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Amount = -100,
            Timestamp = DateTime.UtcNow,
            ProviderName = "Stub"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.Convert(request));
    }

    [Fact]
    public async Task Convert_ThrowsOnFutureTimestamp()
    {
        // Arrange
        var request = new CurrencyConversionRequest
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Amount = 100,
            Timestamp = DateTime.UtcNow.AddDays(1),
            ProviderName = "Stub"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.Convert(request));
    }

    [Fact]
    public async Task Convert_ThrowsOnTooOldTimestamp()
    {
        // Arrange
        var request = new CurrencyConversionRequest
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Amount = 100,
            Timestamp = DateTime.UtcNow.AddYears(-2),
            ProviderName = "Stub"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.Convert(request));
    }

    [Fact]
    public async Task Convert_ThrowsOnEmptyProvider()
    {
        // Arrange
        var request = new CurrencyConversionRequest
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Amount = 100,
            Timestamp = DateTime.UtcNow,
            ProviderName = null
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.Convert(request));
    }

    [Fact]
    public async Task GetLatestRates_ReturnsCorrectRates()
    {
        // Arrange
        var timestamp = DateTime.UtcNow.AddDays(-1);
        _mockProvider
            .Setup(x => x.GetRate("USD", "EUR", timestamp))
            .ReturnsAsync(1.5m);
        _mockProvider
            .Setup(x => x.GetRate("USD", "GBP", timestamp))
            .ReturnsAsync(1.2m);

        var request = new LatestRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrencies = new List<string> { "EUR", "GBP" },
            Timestamp = timestamp,
            ProviderName = "Stub"
        };

        // Act
        var result = await _service.GetLatestRates(request);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(1.5m, result["EUR"]);
        Assert.Equal(1.2m, result["GBP"]);
    }

    [Fact]
    public async Task GetLatestRates_SkipsBaseCurrency()
    {
        // Arrange
        var timestamp = DateTime.UtcNow.AddDays(-1);
        _mockProvider
            .Setup(x => x.GetRate("USD", "EUR", timestamp))
            .ReturnsAsync(1.5m);

        var request = new LatestRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrencies = new List<string> { "EUR", "USD" },
            Timestamp = timestamp,
            ProviderName = "Stub"
        };

        // Act
        var result = await _service.GetLatestRates(request);

        // Assert
        Assert.Single(result);
        Assert.Equal(1.5m, result["EUR"]);
    }

    [Fact]
    public async Task GetLatestRates_ThrowsOnFutureTimestamp()
    {
        // Arrange
        var request = new LatestRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrencies = new List<string> { "EUR" },
            Timestamp = DateTime.UtcNow.AddDays(1),
            ProviderName = "Stub"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetLatestRates(request));
    }

    [Fact]
    public async Task GetLatestRates_ThrowsOnTooOldTimestamp()
    {
        // Arrange
        var request = new LatestRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrencies = new List<string> { "EUR" },
            Timestamp = DateTime.UtcNow.AddYears(-2),
            ProviderName = "Stub"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetLatestRates(request));
    }

    [Fact]
    public async Task GetLatestRates_ThrowsOnEmptyProvider()
    {
        // Arrange
        var request = new LatestRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrencies = new List<string> { "EUR" },
            Timestamp = DateTime.UtcNow,
            ProviderName = null
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetLatestRates(request));
    }

    [Fact]
    public async Task GetLatestRates_ThrowsOnEmptyTargetCurrencies()
    {
        // Arrange
        var request = new LatestRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrencies = new List<string>(),
            Timestamp = DateTime.UtcNow,
            ProviderName = "Stub"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetLatestRates(request));
    }

    [Fact]
    public async Task GetHistoricalRates_ShouldReturnCorrectRates()
    {
        // Arrange
        var request = new HistoricalRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrency = "EUR",
            Start = new DateTime(2025, 1, 1),
            End = new DateTime(2025, 1, 5),
            Page = 1,
            PageSize = 3,
            ProviderName = "Frankfurter"
        };

        var expectedRates = new Dictionary<DateTime, decimal>
        {
            { new DateTime(2025, 1, 1), 1.1m },
            { new DateTime(2025, 1, 2), 1.15m },
            { new DateTime(2025, 1, 3), 1.2m },
            { new DateTime(2025, 1, 4), 1.25m },
            { new DateTime(2025, 1, 5), 1.3m }
        };

        _mockProvider.Setup(p => p.GetRatesForPeriod(
            request.BaseCurrency,
            request.TargetCurrency,
            request.Start,
            request.End))
            .ReturnsAsync(expectedRates);

        // Act
        var result = await _service.GetHistoricalRates(request);

        // Assert
        Assert.Equal(1, result.Page);
        Assert.Equal(3, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
        Assert.Equal(3, result.Rates.Count);
        Assert.Equal(1.1m, result.Rates[new DateTime(2025, 1, 1)]);
        Assert.Equal(1.15m, result.Rates[new DateTime(2025, 1, 2)]);
        Assert.Equal(1.2m, result.Rates[new DateTime(2025, 1, 3)]);
    }

    [Fact]
    public async Task GetHistoricalRates_ThrowsOnFutureTimestamp()
    {
        // Arrange
        var request = new HistoricalRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrency = "EUR",
            Start = DateTime.UtcNow.AddDays(1),
            End = DateTime.UtcNow.AddDays(2),
            Step = TimeSpan.FromDays(1),
            Page = 1,
            PageSize = 10,
            ProviderName = "Stub"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetHistoricalRates(request));
    }

    [Fact]
    public async Task GetHistoricalRates_ThrowsOnTooOldTimestamp()
    {
        // Arrange
        var request = new HistoricalRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrency = "EUR",
            Start = DateTime.UtcNow.AddYears(-2),
            End = DateTime.UtcNow.AddYears(-1),
            Step = TimeSpan.FromDays(1),
            Page = 1,
            PageSize = 10,
            ProviderName = "Stub"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetHistoricalRates(request));
    }

    [Fact]
    public async Task GetHistoricalRates_ThrowsOnStartAfterEnd()
    {
        // Arrange
        var request = new HistoricalRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrency = "EUR",
            Start = DateTime.UtcNow,
            End = DateTime.UtcNow.AddDays(-1),
            Step = TimeSpan.FromDays(1),
            Page = 1,
            PageSize = 10,
            ProviderName = "Stub"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetHistoricalRates(request));
    }

    [Fact]
    public async Task GetHistoricalRates_ThrowsWhenEndDateIsInFuture()
    {
        // Arrange
        var request = new HistoricalRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrency = "EUR",
            Start = DateTime.UtcNow.AddDays(-1),  // yesterday
            End = DateTime.UtcNow.AddDays(1),     // tomorrow
            Step = TimeSpan.FromDays(1),
            Page = 1,
            PageSize = 10,
            ProviderName = "Stub"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.GetHistoricalRates(request));
        Assert.Contains("End date must be between", exception.Message);
    }

    [Fact]
    public async Task GetHistoricalRates_ThrowsWhenStartDateIsInFuture()
    {
        // Arrange
        var request = new HistoricalRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrency = "EUR",
            Start = DateTime.UtcNow.AddDays(1),   // tomorrow
            End = DateTime.UtcNow.AddDays(2),     // day after tomorrow
            Step = TimeSpan.FromDays(1),
            Page = 1,
            PageSize = 10,
            ProviderName = "Stub"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.GetHistoricalRates(request));
        Assert.Contains("Start date must be between", exception.Message);
    }

    [Fact]
    public async Task GetHistoricalRates_ThrowsOnEmptyProvider()
    {
        // Arrange
        var request = new HistoricalRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrency = "EUR",
            Start = DateTime.UtcNow.AddDays(-7),
            End = DateTime.UtcNow.AddDays(-1),
            Step = TimeSpan.FromDays(1),
            Page = 1,
            PageSize = 10,
            ProviderName = null
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetHistoricalRates(request));
    }
}