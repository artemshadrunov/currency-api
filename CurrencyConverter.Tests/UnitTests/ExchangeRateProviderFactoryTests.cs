using ApiCurrency.ExchangeRateProviders;
using ApiCurrency.Services;
using Moq;
using Xunit;

namespace CurrencyConverter.Tests.Services;

public class ExchangeRateProviderFactoryTests
{
    private readonly Mock<IExchangeRateProvider> _mockStubProvider;
    private readonly Mock<IExchangeRateProvider> _mockFrankfurterProvider;
    private readonly ExchangeRateProviderFactory _factory;

    public ExchangeRateProviderFactoryTests()
    {
        _mockStubProvider = new Mock<IExchangeRateProvider>();
        _mockFrankfurterProvider = new Mock<IExchangeRateProvider>();
        _mockStubProvider.Setup(p => p.Name).Returns("stub");
        _mockFrankfurterProvider.Setup(p => p.Name).Returns("frankfurter");
        _factory = new ExchangeRateProviderFactory(new[] { _mockStubProvider.Object, _mockFrankfurterProvider.Object });
    }

    [Theory]
    [InlineData("Stub", true)]
    [InlineData("stub", true)]
    [InlineData("Frankfurter", false)]
    [InlineData("frankfurter", false)]
    public void GetProvider_ShouldReturnCorrectProvider(string providerName, bool shouldReturnStub)
    {
        // Act
        var result = _factory.GetProvider(providerName);

        // Assert
        Assert.Equal(shouldReturnStub ? _mockStubProvider.Object : _mockFrankfurterProvider.Object, result);
    }

    [Fact]
    public void GetProvider_WithUnknownProvider_ShouldThrowArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _factory.GetProvider("unknown"));
        Assert.Contains("not found", exception.Message);
    }
}