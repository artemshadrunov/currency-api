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
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 3);
        var step = TimeSpan.FromDays(1);

        // Act
        var result = await _provider.GetRatesForPeriod(from, to, startDate, endDate, step);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result.Values, rate => Assert.Equal(1.5m, rate));
    }

    [Fact]
    public async Task GetRatesForPeriod_WithDifferentStep_ShouldReturnCorrectNumberOfRates()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 3);
        var step = TimeSpan.FromDays(1);

        // Act
        var result = await _provider.GetRatesForPeriod(from, to, startDate, endDate, step);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result.Values, rate => Assert.Equal(1.5m, rate));
        Assert.Contains(startDate, result.Keys);
        Assert.Contains(startDate.AddDays(1), result.Keys);
        Assert.Contains(endDate, result.Keys);
    }
}