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

namespace CurrencyConverter.Tests.UnitTests
{
    public class CachedExchangeRateProviderSegmentsTests
    {
        private readonly Mock<IExchangeRateProvider> _mockProvider;
        private readonly Mock<ICacheProvider> _mockCache;
        private readonly CachedExchangeRateProvider _provider;
        private readonly RedisSettings _settings;

        public CachedExchangeRateProviderSegmentsTests()
        {
            _mockProvider = new Mock<IExchangeRateProvider>();
            _mockCache = new Mock<ICacheProvider>();
            _settings = new RedisSettings { CacheRetentionDays = 30 };
            _provider = new CachedExchangeRateProvider(
                _mockProvider.Object,
                _mockCache.Object,
                Options.Create(_settings));
        }

        [Fact]
        public async Task GetRatesForPeriod_ShouldGroupDatesIntoSegments()
        {
            // Arrange
            var from = "USD";
            var to = "EUR";
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 1, 7);

            // Only January 2nd and 5th are in cache
            var cache = new Dictionary<string, decimal>();
            _mockCache.Setup(c => c.Get<decimal>(It.IsAny<string>()))
                .ReturnsAsync((string key) => cache.TryGetValue(key, out var val) ? val : default);
            _mockCache.Setup(c => c.Set(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<TimeSpan?>()))
                .Callback((string key, decimal value, TimeSpan? _) => cache[key] = value)
                .Returns(Task.CompletedTask);
            cache[$"exchange_rate_{from}_{to}_2024-01-02"] = 1.1m;
            cache[$"exchange_rate_{from}_{to}_2024-01-05"] = 1.2m;

            // All values for the date range
            var allData = new Dictionary<DateTime, decimal>
            {
                { new DateTime(2024, 1, 1), 1.0m },
                { new DateTime(2024, 1, 3), 1.15m },
                { new DateTime(2024, 1, 4), 1.18m },
                { new DateTime(2024, 1, 6), 1.25m },
                { new DateTime(2024, 1, 7), 1.28m }
            };

            // Mock returns only dates that exist in allData and fall within the range
            _mockProvider.Setup(p => p.GetRatesForPeriod(from, to, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync((string f, string t, DateTime start, DateTime end) =>
                {
                    var result = new Dictionary<DateTime, decimal>();
                    for (var date = start; date <= end; date = date.AddDays(1))
                        if (allData.TryGetValue(date, out var value))
                            result[date] = value;
                    return result;
                });

            // Act
            var result = await _provider.GetRatesForPeriod(from, to, startDate, endDate);

            // Assert
            Assert.Equal(7, result.Count); // 7 days total
            Assert.Equal(1.0m, result[new DateTime(2024, 1, 1)]); // from provider
            Assert.Equal(1.1m, result[new DateTime(2024, 1, 2)]); // from cache
            Assert.Equal(1.15m, result[new DateTime(2024, 1, 3)]); // from provider
            Assert.Equal(1.18m, result[new DateTime(2024, 1, 4)]); // from provider
            Assert.Equal(1.2m, result[new DateTime(2024, 1, 5)]); // from cache
            Assert.Equal(1.25m, result[new DateTime(2024, 1, 6)]); // from provider
            Assert.Equal(1.28m, result[new DateTime(2024, 1, 7)]); // from provider

            // Verify provider was called for Jan 1st, Jan 3rd-4th, and Jan 6th-7th
            _mockProvider.Verify(p => p.GetRatesForPeriod(from, to, new DateTime(2024, 1, 1), new DateTime(2024, 1, 1)), Times.Once);
            _mockProvider.Verify(p => p.GetRatesForPeriod(from, to, new DateTime(2024, 1, 3), new DateTime(2024, 1, 4)), Times.Once);
            _mockProvider.Verify(p => p.GetRatesForPeriod(from, to, new DateTime(2024, 1, 6), new DateTime(2024, 1, 7)), Times.Once);
            _mockProvider.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetRatesForPeriod_ShouldHandleSingleDateSegment()
        {
            // Arrange
            var from = "USD";
            var to = "EUR";
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 1, 3);
            var cache = new Dictionary<string, decimal>
            {
                { $"exchange_rate_{from}_{to}_2024-01-02", 1.1m }
            };
            _mockCache.Setup(c => c.Get<decimal>(It.IsAny<string>()))
                .ReturnsAsync((string key) => cache.TryGetValue(key, out var val) ? val : default);
            _mockCache.Setup(c => c.Set(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<TimeSpan?>()))
                .Callback((string key, decimal value, TimeSpan? _) => cache[key] = value)
                .Returns(Task.CompletedTask);
            var allData = new Dictionary<DateTime, decimal>
            {
                { new DateTime(2024, 1, 1), 1.0m },
                { new DateTime(2024, 1, 3), 1.2m }
            };
            _mockProvider.Setup(p => p.GetRatesForPeriod(from, to, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync((string f, string t, DateTime start, DateTime end) =>
                {
                    var result = new Dictionary<DateTime, decimal>();
                    for (var date = start; date <= end; date = date.AddDays(1))
                        if (allData.TryGetValue(date, out var value))
                            result[date] = value;
                    return result;
                });
            // Act
            var result = await _provider.GetRatesForPeriod(from, to, startDate, endDate);
            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal(1.0m, result[new DateTime(2024, 1, 1)]);
            Assert.Equal(1.1m, result[new DateTime(2024, 1, 2)]);
            Assert.Equal(1.2m, result[new DateTime(2024, 1, 3)]);
            // Verify provider was called for Jan 1st and Jan 3rd
            _mockProvider.Verify(p => p.GetRatesForPeriod(from, to, new DateTime(2024, 1, 1), new DateTime(2024, 1, 1)), Times.Once);
            _mockProvider.Verify(p => p.GetRatesForPeriod(from, to, new DateTime(2024, 1, 3), new DateTime(2024, 1, 3)), Times.Once);
            _mockProvider.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetRatesForPeriod_ShouldHandleEmptyCache()
        {
            // Arrange
            var from = "USD";
            var to = "EUR";
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 1, 3);

            // Setup empty cache
            var cache = new Dictionary<string, decimal>();
            _mockCache.Setup(c => c.Get<decimal>(It.IsAny<string>()))
                .ReturnsAsync((string key) => cache.TryGetValue(key, out var val) ? val : default);

            _mockCache.Setup(c => c.Set(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<TimeSpan?>()))
                .Callback((string key, decimal value, TimeSpan? _) => cache[key] = value)
                .Returns(Task.CompletedTask);

            var expectedData = new Dictionary<DateTime, decimal>
            {
                { new DateTime(2024, 1, 1), 1.0m },
                { new DateTime(2024, 1, 2), 1.1m },
                { new DateTime(2024, 1, 3), 1.2m }
            };

            _mockProvider.Setup(p => p.GetRatesForPeriod(from, to, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync((string f, string t, DateTime start, DateTime end) =>
                {
                    var result = new Dictionary<DateTime, decimal>();
                    for (var date = start; date <= end; date = date.AddDays(1))
                        if (expectedData.TryGetValue(date, out var value))
                            result[date] = value;
                    return result;
                });

            // Act
            var result = await _provider.GetRatesForPeriod(from, to, startDate, endDate);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal(1.0m, result[new DateTime(2024, 1, 1)]);
            Assert.Equal(1.1m, result[new DateTime(2024, 1, 2)]);
            Assert.Equal(1.2m, result[new DateTime(2024, 1, 3)]);

            // Verify all values were cached
            Assert.Equal(3, cache.Count);
            Assert.Equal(1.0m, cache[$"exchange_rate_{from}_{to}_2024-01-01"]);
            Assert.Equal(1.1m, cache[$"exchange_rate_{from}_{to}_2024-01-02"]);
            Assert.Equal(1.2m, cache[$"exchange_rate_{from}_{to}_2024-01-03"]);

            // Verify provider was called once
            _mockProvider.Verify(p => p.GetRatesForPeriod(from, to, startDate, endDate), Times.Once);
            _mockProvider.VerifyNoOtherCalls();
        }
    }
}