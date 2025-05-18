using System.Net;
using CurrencyConverter.Core.Infrastructure.Http;
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

        // Create policies with smaller delays for tests
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
                durationOfBreak: TimeSpan.FromSeconds(1)
            );
        var policyRegistry = new PolicyRegistry { { "CombinedPolicy", circuitBreakerPolicy } };
        var resilientHttpClient = new ResilientHttpClient(httpClient, policyRegistry);

        // Act & Assert
        // First request - should be executed
        var response1 = await resilientHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response1.StatusCode);

        // Second request - should be executed
        var response2 = await resilientHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response2.StatusCode);

        // Third request - should be executed
        var response3 = await resilientHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response3.StatusCode);

        // Fourth request - should be blocked by circuit breaker
        await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(() =>
            resilientHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com")));

        // Wait until circuit breaker resets
        await Task.Delay(TimeSpan.FromSeconds(1.1));

        // Next request should be executed again
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
                durationOfBreak: TimeSpan.FromSeconds(1)
            );
        var policyRegistry = new PolicyRegistry { { "CombinedPolicy", circuitBreakerPolicy } };
        var resilientHttpClient = new ResilientHttpClient(httpClient, policyRegistry);

        // Act: 3 unsuccessful requests
        for (int i = 0; i < 3; i++)
        {
            var response = await resilientHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        // 4th request - circuit breaker should be open
        await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(() =>
            resilientHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com")));

        // Wait for recovery
        await Task.Delay(TimeSpan.FromSeconds(1.1));

        // 5th request - should pass and return success
        var successResponse = await resilientHttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));
        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);
        Assert.Equal(4, attemptCount); // Was 3 failures + 1 success
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