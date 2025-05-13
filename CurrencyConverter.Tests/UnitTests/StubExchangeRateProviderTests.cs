using ApiCurrency.ExchangeRateProviders;
using Xunit;

namespace CurrencyConverter.Tests.UnitTests;

public class StubExchangeRateProviderTests
{
    private readonly StubExchangeRateProvider _provider;

    public StubExchangeRateProviderTests()
    {
        _provider = new StubExchangeRateProvider();
    }

    [Fact]
    public void Name_ShouldReturnStub()
    {
        // Act
        var name = _provider.Name;

        // Assert
        Assert.Equal("Stub", name);
    }

    [Fact]
    public async Task GetRate_ShouldAlwaysReturnOnePointFive()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var date = DateTime.UtcNow.Date;

        // Act
        var result = await _provider.GetRate(from, to, date);

        // Assert
        Assert.Equal(1.5m, result);
    }

    [Fact]
    public async Task GetRatesForPeriod_ShouldReturnCorrectNumberOfRates()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 5);

        // Act
        var result = await _provider.GetRatesForPeriod(from, to, startDate, endDate);

        // Assert
        Assert.Equal(5, result.Count);
        Assert.All(result, kvp => Assert.Equal(1.0m, kvp.Value));
    }

    [Fact]
    public async Task GetRatesForPeriod_WithDifferentStep_ShouldReturnCorrectNumberOfRates()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 5);

        // Act
        var result = await _provider.GetRatesForPeriod(from, to, startDate, endDate);

        // Assert
        Assert.Equal(5, result.Count);
        Assert.All(result, kvp => Assert.Equal(1.0m, kvp.Value));
    }
}