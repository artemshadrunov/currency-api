using Xunit;
using Moq;
using ApiCurrency.ExchangeRateProviders;
using CurrencyConverter.Core.ExchangeRateProviders;
using CurrencyConverter.Core.Infrastructure.Cache;
using CurrencyConverter.Core.Settings;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Tests.UnitTests
{
    public class CachedExchangeRateProviderTests
    {
        private readonly Mock<IExchangeRateProvider> _mockProvider;
        private readonly Mock<ICacheProvider> _mockCache;
        private readonly CachedExchangeRateProvider _provider;
        private readonly Mock<ILogger<CachedExchangeRateProvider>> _mockLogger;

        public CachedExchangeRateProviderTests()
        {
            _mockProvider = new Mock<IExchangeRateProvider>();
            _mockCache = new Mock<ICacheProvider>();
            _mockLogger = new Mock<ILogger<CachedExchangeRateProvider>>();
            _provider = new CachedExchangeRateProvider(
                _mockProvider.Object,
                _mockCache.Object,
                Options.Create(new RedisSettings()),
                _mockLogger.Object);
        }

        [Fact]
        public async Task GetRatesForPeriod_ShouldCacheResults()
        {
            // Arrange
            var from = "USD";
            var to = "EUR";
            var startDate = new DateTime(2025, 1, 1);
            var endDate = new DateTime(2025, 1, 5);
            var expected = new Dictionary<DateTime, decimal>
            {
                { new DateTime(2025, 1, 1), 1.0m },
                { new DateTime(2025, 1, 2), 1.0m },
                { new DateTime(2025, 1, 3), 1.0m },
                { new DateTime(2025, 1, 4), 1.0m },
                { new DateTime(2025, 1, 5), 1.0m }
            };
            // First cache miss, then cache hit
            var cache = new Dictionary<string, decimal>();
            _mockCache.Setup(c => c.Get<decimal>(It.IsAny<string>())).ReturnsAsync((string key) =>
            {
                if (cache.TryGetValue(key, out var val))
                    return val;
                return default;
            });
            _mockCache.Setup(c => c.Set(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<TimeSpan?>()))
                .Callback((string key, decimal value, TimeSpan? _) => cache[key] = value)
                .Returns(Task.CompletedTask);
            _mockProvider.Setup(p => p.GetRatesForPeriod(from, to, startDate, endDate)).ReturnsAsync(expected);

            // Act
            var result1 = await _provider.GetRatesForPeriod(from, to, startDate, endDate);
            var result2 = await _provider.GetRatesForPeriod(from, to, startDate, endDate);

            // Assert
            Assert.Equal(result1, result2);
            _mockProvider.Verify(p => p.GetRatesForPeriod(from, to, startDate, endDate), Times.Once);
        }
    }
}