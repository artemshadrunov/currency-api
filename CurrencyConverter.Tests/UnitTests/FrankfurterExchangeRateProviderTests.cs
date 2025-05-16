using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CurrencyConverter.Core.ExchangeRateProviders;
using Moq;
using Moq.Protected;
using Xunit;
using Microsoft.Extensions.Logging;
using CurrencyConverter.Core.Infrastructure.Http;

namespace CurrencyConverter.Tests.UnitTests;

public class FrankfurterExchangeRateProviderTests
{
    private readonly Mock<IHttpClient> _mockHttpClient;
    private readonly FrankfurterExchangeRateProvider _provider;
    private readonly Mock<ILogger<FrankfurterExchangeRateProvider>> _mockLogger;

    public FrankfurterExchangeRateProviderTests()
    {
        _mockHttpClient = new Mock<IHttpClient>();
        _mockLogger = new Mock<ILogger<FrankfurterExchangeRateProvider>>();
        _provider = new FrankfurterExchangeRateProvider(_mockHttpClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetRate_WhenDateIsToday_ShouldUseLatestEndpoint()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var date = DateTime.UtcNow.Date;
        var expectedRate = 1.1m;

        SetupMockResponse($"https://api.frankfurter.app/latest?from={from}&to={to}", new FrankfurterResponse
        {
            Rates = new Dictionary<string, decimal> { { to, expectedRate } }
        });

        // Act
        var result = await _provider.GetRate(from, to, date);

        // Assert
        Assert.Equal(expectedRate, result);
        VerifyHttpCall($"https://api.frankfurter.app/latest?from={from}&to={to}");
    }

    [Fact]
    public async Task GetRate_WhenDateIsNotToday_ShouldUseHistoricalEndpoint()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var date = DateTime.UtcNow.Date.AddDays(-1);
        var expectedRate = 1.2m;

        SetupMockResponse($"https://api.frankfurter.app/{date:yyyy-MM-dd}?from={from}&to={to}", new FrankfurterResponse
        {
            Rates = new Dictionary<string, decimal> { { to, expectedRate } }
        });

        // Act
        var result = await _provider.GetRate(from, to, date);

        // Assert
        Assert.Equal(expectedRate, result);
        VerifyHttpCall($"https://api.frankfurter.app/{date:yyyy-MM-dd}?from={from}&to={to}");
    }

    [Fact]
    public async Task GetRatesForPeriod_ShouldReturnCorrectNumberOfRates()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 5);
        var rates = new Dictionary<string, Dictionary<string, decimal>>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            rates[date.ToString("yyyy-MM-dd")] = new Dictionary<string, decimal> { { to, 1.0m } };
        }
        SetupMockResponse($"https://api.frankfurter.app/{startDate:yyyy-MM-dd}..{endDate:yyyy-MM-dd}?from={from}&to={to}", new FrankfurterHistoricalResponse { Rates = rates });

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
        var rates = new Dictionary<string, Dictionary<string, decimal>>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            rates[date.ToString("yyyy-MM-dd")] = new Dictionary<string, decimal> { { to, 1.0m } };
        }
        SetupMockResponse($"https://api.frankfurter.app/{startDate:yyyy-MM-dd}..{endDate:yyyy-MM-dd}?from={from}&to={to}", new FrankfurterHistoricalResponse { Rates = rates });

        // Act
        var result = await _provider.GetRatesForPeriod(from, to, startDate, endDate);

        // Assert
        Assert.Equal(5, result.Count);
        Assert.All(result, kvp => Assert.Equal(1.0m, kvp.Value));
    }

    [Fact]
    public async Task GetRate_WhenApiFails_ShouldThrowException()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var date = DateTime.UtcNow.Date;

        _mockHttpClient
            .Setup(x => x.SendAsync(
                It.Is<HttpRequestMessage>(req => req.RequestUri.ToString() == $"https://api.frankfurter.app/latest?from={from}&to={to}"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetRate(from, to, date));
    }

    private void SetupMockResponse(string url, object response)
    {
        _mockHttpClient
            .Setup(x => x.SendAsync(
                It.Is<HttpRequestMessage>(req => req.RequestUri.ToString() == url),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = response != null ? JsonContent.Create(response) : null
            });
    }

    private void VerifyHttpCall(string url)
    {
        _mockHttpClient.Verify(
            x => x.SendAsync(
                It.Is<HttpRequestMessage>(req => req.RequestUri.ToString() == url),
                It.IsAny<CancellationToken>()),
            Times.Once());
    }
}