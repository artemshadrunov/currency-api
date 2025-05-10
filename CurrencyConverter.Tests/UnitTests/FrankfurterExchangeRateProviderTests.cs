using System.Net;
using System.Net.Http.Json;
using ApiCurrency.ExchangeRateProviders;
using ApiCurrency.Settings;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace CurrencyConverter.Tests.UnitTests;

public class FrankfurterExchangeRateProviderTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly FrankfurterExchangeRateProvider _provider;
    private readonly ExchangeRateSettings _settings;

    public FrankfurterExchangeRateProviderTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _settings = new ExchangeRateSettings { FreshnessWindowMinutes = 10 };
        var mockOptions = new Mock<IOptions<ExchangeRateSettings>>();
        mockOptions.Setup(x => x.Value).Returns(_settings);
        _provider = new FrankfurterExchangeRateProvider(_httpClient, mockOptions.Object);
    }

    [Fact]
    public async Task GetRate_WhenDateIsRecent_ShouldUseLatestEndpoint()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var date = DateTime.UtcNow;
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
    public async Task GetRate_WhenDateIsOld_ShouldUseHistoricalEndpoint()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var date = DateTime.UtcNow.AddMinutes(-20);
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
    public async Task GetRatesForPeriod_ShouldReturnCorrectRates()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var startDate = new DateTime(2025, 5, 13);
        var endDate = new DateTime(2025, 5, 15);
        var step = TimeSpan.FromDays(1);

        var expectedRates = new Dictionary<string, Dictionary<string, decimal>>
        {
            { "2025-05-13", new Dictionary<string, decimal> { { to, 1.1m } } },
            { "2025-05-14", new Dictionary<string, decimal> { { to, 1.15m } } },
            { "2025-05-15", new Dictionary<string, decimal> { { to, 1.2m } } }
        };

        SetupMockResponse(
            $"https://api.frankfurter.app/{startDate:yyyy-MM-dd}..?from={from}&to={to}&start_date={startDate:yyyy-MM-dd}&end_date={endDate:yyyy-MM-dd}",
            new FrankfurterHistoricalResponse { Rates = expectedRates });

        // Act
        var result = await _provider.GetRatesForPeriod(from, to, startDate, endDate, step);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(1.1m, result[startDate]);
        Assert.Equal(1.15m, result[startDate.AddDays(1)]);
        Assert.Equal(1.2m, result[endDate]);
    }

    [Fact]
    public async Task GetRate_WhenApiFails_ShouldThrowException()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var date = DateTime.UtcNow;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString() == $"https://api.frankfurter.app/latest?from={from}&to={to}"),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetRate(from, to, date));
    }

    private void SetupMockResponse(string url, object response)
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString() == url),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = response != null ? JsonContent.Create(response) : null
            });
    }

    private void VerifyHttpCall(string url)
    {
        _mockHttpMessageHandler
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString() == url),
                ItExpr.IsAny<CancellationToken>()
            );
    }
}