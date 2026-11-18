using System.Text.Json;
using StackExchange.Redis;

namespace TicketRush.Shared.Caching;

public interface ICacheService
{
    Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, TimeSpan ttl, CancellationToken cancellationToken);
    Task RemoveAsync(params string[] keys);
}

/// <summary>Cache-aside simples em Redis (JSON).</summary>
internal sealed class RedisCacheService(IConnectionMultiplexer redis) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync(key);
        if (cached.HasValue)
        {
            return JsonSerializer.Deserialize<T>(cached.ToString(), JsonOptions);
        }

        var value = await factory(cancellationToken);
        if (value is not null)
        {
            await db.StringSetAsync(key, JsonSerializer.Serialize(value, JsonOptions), ttl);
        }

        return value;
    }

    public Task RemoveAsync(params string[] keys) =>
        redis.GetDatabase().KeyDeleteAsync(keys.Select(k => (RedisKey)k).ToArray());
}
