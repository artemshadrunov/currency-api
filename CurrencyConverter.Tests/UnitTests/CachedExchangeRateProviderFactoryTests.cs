using System;
using System.Collections.Generic;
using Xunit;
using Moq;
using CurrencyConverter.Core.ExchangeRateProviders;
using CurrencyConverter.Core.Infrastructure.Cache;
using CurrencyConverter.Core.Settings;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Tests.UnitTests
{
    public class CachedExchangeRateProviderFactoryTests
    {
        private readonly Mock<ICacheProvider> _mockCache = new();
        private readonly RedisSettings _settings = new() { CacheRetentionDays = 30 };
        private readonly Mock<ILogger<CachedExchangeRateProvider>> _mockLogger = new();

        private CachedExchangeRateProviderFactory CreateFactory(params IExchangeRateProvider[] providers)
        {
            return new CachedExchangeRateProviderFactory(
                _mockCache.Object,
                Options.Create(_settings),
                providers,
                _mockLogger.Object);
        }

        [Fact]
        public void GetProvider_ThrowsArgumentException_WhenNameIsNullOrEmpty()
        {
            var factory = CreateFactory();
            Assert.Throws<ArgumentException>(() => factory.GetProvider(null));
            Assert.Throws<ArgumentException>(() => factory.GetProvider(""));
        }

        [Fact]
        public void GetProvider_ThrowsInvalidOperationException_WhenProviderNotFound()
        {
            var stub = new StubExchangeRateProvider();
            var cached = new CachedExchangeRateProvider(stub, _mockCache.Object, Options.Create(_settings), _mockLogger.Object);
            var factory = CreateFactory(cached);
            Assert.Throws<InvalidOperationException>(() => factory.GetProvider("notexists"));
        }

        [Fact]
        public void GetProvider_ReturnsProvider_WhenExists()
        {
            var stub = new StubExchangeRateProvider();
            var cached = new CachedExchangeRateProvider(stub, _mockCache.Object, Options.Create(_settings), _mockLogger.Object);
            var factory = CreateFactory(cached);
            var result = factory.GetProvider(stub.Name);
            Assert.NotNull(result);
            Assert.IsType<CachedExchangeRateProvider>(result);
        }

        [Fact]
        public void CreateCachedProvider_ReturnsCachedProvider()
        {
            var stub = new StubExchangeRateProvider();
            var factory = CreateFactory();
            var result = factory.CreateCachedProvider(stub);
            Assert.NotNull(result);
            Assert.IsType<CachedExchangeRateProvider>(result);
        }
    }
}