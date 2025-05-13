using System.Text.Json;
using CurrencyConverter.Core.Settings;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace CurrencyConverter.Core.Infrastructure.Cache;

public class RedisCacheProvider : ICacheProvider
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly RedisSettings _settings;

    public RedisCacheProvider(IConnectionMultiplexer redis, IOptions<RedisSettings> settings)
    {
        _redis = redis;
        _db = redis.GetDatabase();
        _settings = settings.Value;
    }

    public async Task<T?> Get<T>(string key)
    {
        try
        {
            Console.WriteLine($"[Cache] Trying to get value for key: {key}");
            var value = await _db.StringGetAsync(GetKey(key));
            if (!value.HasValue)
            {
                Console.WriteLine($"[Cache] Miss for key: {key}");
                return default;
            }

            Console.WriteLine($"[Cache] Hit for key: {key}");
            return JsonSerializer.Deserialize<T>(value!);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Cache] Error getting value for key {key}: {ex.Message}");
            return default;
        }
    }

    public async Task Set<T>(string key, T value, TimeSpan? expiration = null)
    {
        try
        {
            Console.WriteLine($"[Cache] Setting value for key: {key}");
            var serializedValue = JsonSerializer.Serialize(value);
            var finalKey = GetKey(key);

            await _db.StringSetAsync(
                finalKey,
                serializedValue,
                expiration ?? TimeSpan.FromDays(_settings.CacheRetentionDays)
            );
            Console.WriteLine($"[Cache] Successfully set value for key: {key}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Cache] Error setting value for key {key}: {ex.Message}");
        }
    }

    public async Task Remove(string key)
    {
        try
        {
            Console.WriteLine($"[Cache] Removing value for key: {key}");
            await _db.KeyDeleteAsync(GetKey(key));
            Console.WriteLine($"[Cache] Successfully removed value for key: {key}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Cache] Error removing value for key {key}: {ex.Message}");
        }
    }

    public async Task<bool> Exists(string key)
    {
        try
        {
            return await _db.KeyExistsAsync(GetKey(key));
        }
        catch
        {
            return false;
        }
    }

    public async Task Clear()
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{_settings.InstanceName}*");
            foreach (var key in keys)
            {
                await _db.KeyDeleteAsync(key);
            }
        }
        catch
        {
            // Log error if needed
        }
    }

    private string GetKey(string key) => $"{_settings.InstanceName}{key}";
}