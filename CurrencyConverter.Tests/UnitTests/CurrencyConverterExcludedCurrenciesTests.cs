using ApiCurrency.ExchangeRateProviders;
using ApiCurrency.Models;
using ApiCurrency.Services;
using ApiCurrency.Settings;
using Moq;
using Xunit;

namespace CurrencyConverter.Tests.Services;

public class CurrencyConverterExcludedCurrenciesTests
{
    private readonly Mock<IExchangeRateProviderFactory> _mockProviderFactory;
    private readonly Mock<IExchangeRateProvider> _mockProvider;
    private readonly Mock<ICurrencyRulesProvider> _mockCurrencyRulesProvider;
    private readonly CurrencyConverterService _service;

    public CurrencyConverterExcludedCurrenciesTests()
    {
        _mockProviderFactory = new Mock<IExchangeRateProviderFactory>();
        _mockProvider = new Mock<IExchangeRateProvider>();
        _mockCurrencyRulesProvider = new Mock<ICurrencyRulesProvider>();
        _service = new CurrencyConverterService(_mockProviderFactory.Object, _mockCurrencyRulesProvider.Object);

        _mockProviderFactory
            .Setup(x => x.GetProvider(It.IsAny<string>()))
            .Returns(_mockProvider.Object);

        _mockProvider
            .Setup(x => x.GetRate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(1.5m);

        _mockCurrencyRulesProvider
            .Setup(x => x.IsCurrencyExcluded(It.IsAny<string>()))
            .Returns(false);
    }

    [Fact]
    public async Task Convert_WhenFromCurrencyIsExcluded_ShouldThrowException()
    {
        // Arrange
        var request = new CurrencyConversionRequest
        {
            FromCurrency = "TRY",
            ToCurrency = "USD",
            Amount = 100,
            Timestamp = DateTime.UtcNow.AddDays(-1),
            ProviderName = "Frankfurter"
        };

        _mockCurrencyRulesProvider
            .Setup(x => x.IsCurrencyExcluded("TRY"))
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.Convert(request));
        Assert.Equal("Currency TRY is excluded from conversion", exception.Message);
    }

    [Fact]
    public async Task Convert_WhenToCurrencyIsExcluded_ShouldThrowException()
    {
        // Arrange
        var request = new CurrencyConversionRequest
        {
            FromCurrency = "USD",
            ToCurrency = "PLN",
            Amount = 100,
            Timestamp = DateTime.UtcNow.AddDays(-1),
            ProviderName = "Frankfurter"
        };

        _mockCurrencyRulesProvider
            .Setup(x => x.IsCurrencyExcluded("PLN"))
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.Convert(request));
        Assert.Equal("Currency PLN is excluded from conversion", exception.Message);
    }

    [Fact]
    public async Task Convert_WhenBothCurrenciesAreExcluded_ShouldThrowExceptionForFromCurrency()
    {
        // Arrange
        var request = new CurrencyConversionRequest
        {
            FromCurrency = "TRY",
            ToCurrency = "PLN",
            Amount = 100,
            Timestamp = DateTime.UtcNow.AddDays(-1),
            ProviderName = "Frankfurter"
        };

        _mockCurrencyRulesProvider
            .Setup(x => x.IsCurrencyExcluded("TRY"))
            .Returns(true);
        _mockCurrencyRulesProvider
            .Setup(x => x.IsCurrencyExcluded("PLN"))
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.Convert(request));
        Assert.Equal("Currency TRY is excluded from conversion", exception.Message);
    }

    [Fact]
    public async Task Convert_WhenCurrenciesAreNotExcluded_ShouldReturnResult()
    {
        // Arrange
        var timestamp = DateTime.UtcNow.AddDays(-1);
        var request = new CurrencyConversionRequest
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Amount = 100,
            Timestamp = timestamp,
            ProviderName = "Frankfurter"
        };

        // Act
        var result = await _service.Convert(request);

        // Assert
        Assert.Equal("USD", result.FromCurrency);
        Assert.Equal("EUR", result.ToCurrency);
        Assert.Equal(100, result.Amount);
        Assert.Equal(150, result.ConvertedAmount);
        Assert.Equal(1.5m, result.Rate);
        Assert.Equal("Frankfurter", result.ProviderName);
        Assert.Equal(timestamp, result.ConversionTimestamp);
    }
}