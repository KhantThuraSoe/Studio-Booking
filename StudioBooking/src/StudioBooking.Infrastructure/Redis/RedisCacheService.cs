using System.Text.Json;
using StudioBooking.Application.Interfaces;
using StackExchange.Redis;

namespace StudioBooking.Infrastructure.Redis;

public class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IConnectionMultiplexer _redis;

    public RedisCacheService(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(key);
        if (value.IsNullOrEmpty)
            return default;

        return JsonSerializer.Deserialize<T>(value!, JsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await db.StringSetAsync(key, json, expiry);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(key);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var server = _redis.GetServers().FirstOrDefault();
        if (server is null)
            return;

        var db = _redis.GetDatabase();
        foreach (var key in server.Keys(pattern: $"{prefix}*"))
            await db.KeyDeleteAsync(key);
    }
}
