using System.Net;
using System.Net.Http;
using CurrencyConverter.Core.Infrastructure.Http;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Polly.Registry;
using Polly.CircuitBreaker;
using Xunit;

namespace CurrencyConverter.Tests.UnitTests;

public class ResiliencePoliciesTests
{
    private readonly IHttpClient _httpClient;
    private readonly MockHttpMessageHandler _mockHttpMessageHandler;
    private readonly PolicyRegistry _policyRegistry;

    public ResiliencePoliciesTests()
    {
        _mockHttpMessageHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(_mockHttpMessageHandler);

        // Создаем политики с меньшими задержками для тестов
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt)));

        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(10)
            );

        var combinedPolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

        _policyRegistry = new PolicyRegistry
        {
            { "CombinedPolicy", combinedPolicy }
        };

        _httpClient = new ResilientHttpClient(httpClient, _policyRegistry);
    }

    [Fact]
    public async Task RetryPolicy_ShouldRetryOnTransientError()
    {
        // Arrange
        var attemptCount = 0;
        _mockHttpMessageHandler.SetupResponse(req =>
        {
            attemptCount++;
            return attemptCount <= 2
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        // Act
        var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task CircuitBreaker_ShouldBreakCircuitAfterThreeFailures()
    {
        // Arrange
        var attemptCount = 0;
        _mockHttpMessageHandler.SetupResponse(req =>
        {
            attemptCount++;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });

        var httpClient = new HttpClient(_mockHttpMessageHandler);
        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(10)
            );
        var policyRegistry = new PolicyRegistry { { "CombinedPolicy", circuitBreakerPolicy } };
        var resilientHttpClient = new ResilientHttpClient(httpClient, policyRegistry);

        // Act & Assert
        // Первый запрос - должен быть выполнен
        var response1 = await resilientHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response1.StatusCode);

        // Второй запрос - должен быть выполнен
        var response2 = await resilientHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response2.StatusCode);

        // Третий запрос - должен быть выполнен
        var response3 = await resilientHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response3.StatusCode);

        // Четвертый запрос - должен быть заблокирован размыкателем цепи
        await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(() =>
            resilientHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com")));

        // Ждем, пока размыкатель цепи сбросится
        await Task.Delay(TimeSpan.FromSeconds(10.1));

        // Следующий запрос должен снова быть выполнен
        var response5 = await resilientHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response5.StatusCode);
    }

    [Fact]
    public async Task CircuitBreaker_ShouldRecoverAfterBreakAndReturnSuccess()
    {
        // Arrange
        var attemptCount = 0;
        _mockHttpMessageHandler.SetupResponse(req =>
        {
            attemptCount++;
            if (attemptCount <= 3)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = new HttpClient(_mockHttpMessageHandler);
        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(10)
            );
        var policyRegistry = new PolicyRegistry { { "CombinedPolicy", circuitBreakerPolicy } };
        var resilientHttpClient = new ResilientHttpClient(httpClient, policyRegistry);

        // Act: 3 неудачных запроса
        for (int i = 0; i < 3; i++)
        {
            var response = await resilientHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        // 4-й запрос — circuit breaker должен быть открыт
        await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(() =>
            resilientHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com")));

        // Ждём восстановления
        await Task.Delay(TimeSpan.FromSeconds(10.1));

        // 5-й запрос — должен пройти и вернуть успех
        var successResponse = await resilientHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));
        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);
        Assert.Equal(4, attemptCount); // Было 3 неудачи + 1 успех
    }

    [Fact]
    public async Task CombinedPolicy_ShouldHandleSuccessAfterRetries()
    {
        // Arrange
        var attemptCount = 0;
        _mockHttpMessageHandler.SetupResponse(req =>
        {
            attemptCount++;
            return attemptCount <= 2
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        // Act
        var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, attemptCount);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public MockHttpMessageHandler()
        {
            _responseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK);
        }

        public void SetupResponse(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }
}