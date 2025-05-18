using System;
using System.Threading.Tasks;
using System.Net;
using Xunit;
using Moq;
using StackExchange.Redis;
using CurrencyConverter.Core.Infrastructure.Cache;
using CurrencyConverter.Core.Settings;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CurrencyConverter.Tests.UnitTests
{
    public class RedisCacheProviderTests
    {
        private readonly Mock<IDatabase> _mockDb;
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly RedisSettings _settings;
        private readonly RedisCacheProvider _cacheProvider;

        public RedisCacheProviderTests()
        {
            _mockDb = new Mock<IDatabase>();
            _settings = new RedisSettings { InstanceName = "test_" };
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockRedis.Setup(x => x.GetDatabase(It.Is<int>(db => true), It.Is<object?>(state => true)))
                .Returns(_mockDb.Object);

            _cacheProvider = new RedisCacheProvider(
                _mockRedis.Object,
                Options.Create(_settings));
        }

        [Fact]
        public async Task Get_WhenKeyExists_ReturnsValue()
        {
            // Arrange
            var key = "test_key";
            var value = 123;
            var serializedValue = JsonSerializer.Serialize(value);
            var fullKey = $"{_settings.InstanceName}{key}";

            _mockDb.Setup(x => x.StringGetAsync(fullKey, CommandFlags.None))
                .ReturnsAsync(serializedValue);

            // Act
            var result = await _cacheProvider.Get<int>(key);

            // Assert
            Assert.Equal(value, result);
        }

        [Fact]
        public async Task Get_WhenKeyDoesNotExist_ReturnsDefault()
        {
            // Arrange
            var key = "test_key";
            var fullKey = $"{_settings.InstanceName}{key}";

            _mockDb.Setup(x => x.StringGetAsync(fullKey, CommandFlags.None))
                .ReturnsAsync(RedisValue.Null);

            // Act
            var result = await _cacheProvider.Get<int>(key);

            // Assert
            Assert.Equal(default, result);
        }

        [Fact]
        public async Task Remove_DeletesKey()
        {
            // Arrange
            var key = "test_key";
            var fullKey = $"{_settings.InstanceName}{key}";

            _mockDb.Setup(x => x.KeyDeleteAsync(fullKey, CommandFlags.None))
                .ReturnsAsync(true);

            // Act
            await _cacheProvider.Remove(key);

            // Assert
            _mockDb.Verify(x => x.KeyDeleteAsync(fullKey, CommandFlags.None), Times.Once);
        }

        [Fact]
        public async Task Exists_WhenKeyExists_ReturnsTrue()
        {
            // Arrange
            var key = "test_key";
            var fullKey = $"{_settings.InstanceName}{key}";

            _mockDb.Setup(x => x.KeyExistsAsync(fullKey, CommandFlags.None))
                .ReturnsAsync(true);

            // Act
            var result = await _cacheProvider.Exists(key);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task Exists_WhenKeyDoesNotExist_ReturnsFalse()
        {
            // Arrange
            var key = "test_key";
            var fullKey = $"{_settings.InstanceName}{key}";

            _mockDb.Setup(x => x.KeyExistsAsync(fullKey, CommandFlags.None))
                .ReturnsAsync(false);

            // Act
            var result = await _cacheProvider.Exists(key);

            // Assert
            Assert.False(result);
        }
    }
}