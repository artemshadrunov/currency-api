using Xunit;
using CurrencyConverter.Core.ExchangeRateProviders;
using System;

namespace CurrencyConverter.Tests.UnitTests
{
    public class DateTimeExtensionsTests
    {
        [Fact]
        public void RoundDown_WithHourInterval_ShouldRoundToNearestHour()
        {
            // Arrange
            var dateTime = new DateTime(2024, 1, 1, 14, 30, 45);
            var interval = TimeSpan.FromHours(1);

            // Act
            var result = dateTime.RoundDown(interval);

            // Assert
            Assert.Equal(new DateTime(2024, 1, 1, 14, 0, 0), result);
        }

        [Fact]
        public void RoundDown_WithDayInterval_ShouldRoundToNearestDay()
        {
            // Arrange
            var dateTime = new DateTime(2024, 1, 1, 14, 30, 45);
            var interval = TimeSpan.FromDays(1);

            // Act
            var result = dateTime.RoundDown(interval);

            // Assert
            Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0), result);
        }

        [Fact]
        public void RoundDown_WithMinuteInterval_ShouldRoundToNearestMinute()
        {
            // Arrange
            var dateTime = new DateTime(2024, 1, 1, 14, 30, 45);
            var interval = TimeSpan.FromMinutes(15);

            // Act
            var result = dateTime.RoundDown(interval);

            // Assert
            Assert.Equal(new DateTime(2024, 1, 1, 14, 30, 0), result);
        }

        [Fact]
        public void RoundDown_WithExactInterval_ShouldReturnSameDateTime()
        {
            // Arrange
            var dateTime = new DateTime(2024, 1, 1, 14, 0, 0);
            var interval = TimeSpan.FromHours(1);

            // Act
            var result = dateTime.RoundDown(interval);

            // Assert
            Assert.Equal(dateTime, result);
        }

        [Fact]
        public void RoundDown_WithZeroInterval_ShouldThrowArgumentException()
        {
            // Arrange
            var dateTime = new DateTime(2024, 1, 1, 14, 30, 45);
            var interval = TimeSpan.Zero;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => dateTime.RoundDown(interval));
        }

        [Fact]
        public void RoundDown_WithNegativeInterval_ShouldThrowArgumentException()
        {
            // Arrange
            var dateTime = new DateTime(2024, 1, 1, 14, 30, 45);
            var interval = TimeSpan.FromHours(-1);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => dateTime.RoundDown(interval));
        }
    }
}