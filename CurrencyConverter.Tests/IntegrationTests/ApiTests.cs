using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CurrencyConverter.Core.Controllers;
using CurrencyConverter.Core.Models;
using Xunit;

namespace CurrencyConverter.Tests.IntegrationTests;

public class ApiTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly string _baseUrl;
    private string? _userToken;
    private string? _adminToken;

    public ApiTests()
    {
        _client = new HttpClient();
        _baseUrl = "https://39tv7m9hl0.execute-api.eu-central-1.amazonaws.com/prod/";
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    [Fact]
    public async Task GetToken_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var request = new TokenRequest { Username = "testuser", Role = "User" };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await _client.PostAsync($"{_baseUrl}/api/auth/token", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        // Debug output
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Status: {response.StatusCode}\nResponse: '{responseContent}'");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(tokenResponse?.Token);
        _userToken = tokenResponse.Token;
    }

    [Fact]
    public async Task GetAdminToken_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var request = new TokenRequest { Username = "admin", Role = "Admin" };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await _client.PostAsync($"{_baseUrl}/api/auth/token", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        // Debug output
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Status: {response.StatusCode}\nResponse: '{responseContent}'");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(tokenResponse?.Token);
        _adminToken = tokenResponse.Token;
    }

    [Fact]
    public async Task ConvertCurrency_ValidRequest_ReturnsConversionResult()
    {
        // Arrange
        await GetToken_ValidCredentials_ReturnsToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _userToken);

        var request = new CurrencyConversionRequest
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Amount = 100,
            Timestamp = DateTime.UtcNow,
            ProviderName = "Frankfurter"
        };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await _client.PostAsync($"{_baseUrl}/api/v1/currencies/convert", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        // Debug output
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Status: {response.StatusCode}\nResponse: '{responseContent}'");
        }

        var result = JsonSerializer.Deserialize<CurrencyConversionResult>(responseContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(request.FromCurrency, result.FromCurrency);
        Assert.Equal(request.ToCurrency, result.ToCurrency);
        Assert.Equal(request.Amount, result.Amount);
        Assert.True(result.ConvertedAmount > 0);
    }

    [Fact]
    public async Task GetLatestRates_ValidRequest_ReturnsRates()
    {
        // Arrange
        await GetToken_ValidCredentials_ReturnsToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _userToken);

        var request = new LatestRatesRequest
        {
            BaseCurrency = "USD",
            TargetCurrencies = new List<string> { "EUR", "GBP", "JPY" },
            Timestamp = DateTime.UtcNow,
            ProviderName = "Frankfurter"
        };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await _client.PostAsync($"{_baseUrl}/api/v1/currencies/latest", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        // Debug output
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Status: {response.StatusCode}\nResponse: '{responseContent}'");
        }

        var result = JsonSerializer.Deserialize<PagedRatesResult>(responseContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.NotNull(result.Rates);
        Assert.True(result.Rates.Count > 0);
    }

    [Fact]
    public async Task GetHistoricalRates_ValidRequest_ReturnsHistoricalRates()
    {
        // Arrange
        await GetAdminToken_ValidCredentials_ReturnsToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var request = new HistoricalRatesRequest
        {
            BaseCurrency = "USD",
            Start = DateTime.UtcNow.AddDays(-7),
            End = DateTime.UtcNow,
            ProviderName = "Frankfurter"
        };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await _client.PostAsync($"{_baseUrl}/api/v1/currencies/history", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        // Debug output
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Status: {response.StatusCode}\nResponse: '{responseContent}'");
        }

        var result = JsonSerializer.Deserialize<PagedRatesResult>(responseContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.NotNull(result.Rates);
        Assert.True(result.Rates.Count > 0);
    }
}

public class TokenResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
}