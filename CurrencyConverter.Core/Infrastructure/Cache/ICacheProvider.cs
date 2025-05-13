namespace CurrencyConverter.Core.Infrastructure.Cache;

public interface ICacheProvider
{
    Task<T?> Get<T>(string key);
    Task Set<T>(string key, T value, TimeSpan? expiration = null);
    Task Remove(string key);
    Task<bool> Exists(string key);
    Task Clear();
}