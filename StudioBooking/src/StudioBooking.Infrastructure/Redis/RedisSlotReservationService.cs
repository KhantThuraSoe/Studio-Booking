using StudioBooking.Application.Interfaces;
using StackExchange.Redis;

namespace StudioBooking.Infrastructure.Redis;

public class RedisSlotReservationService : ISlotReservationService
{
    private const string SlotCountKeyPrefix = "schedule:slots:";
    private const string LockKeyPrefix = "lock:schedule:";

    private readonly IConnectionMultiplexer _redis;

    private const string ReserveSlotScript = @"
local current = redis.call('GET', KEYS[1])
if current == false then
  current = ARGV[2]
  redis.call('SET', KEYS[1], current)
end

current = tonumber(current)
if current >= tonumber(ARGV[1]) then
  return 0
end

redis.call('INCR', KEYS[1])
return 1
";

    public RedisSlotReservationService(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<bool> TryReserveSlotAsync(
        int scheduleId,
        int maxSlots,
        int currentDbCount,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var lockKey = $"{LockKeyPrefix}{scheduleId}";
        var lockToken = Guid.NewGuid().ToString("N");

        var lockAcquired = await db.StringSetAsync(lockKey, lockToken, TimeSpan.FromSeconds(10), When.NotExists);
        if (!lockAcquired)
            return false;

        try
        {
            var countKey = $"{SlotCountKeyPrefix}{scheduleId}";
            var result = await db.ScriptEvaluateAsync(
                ReserveSlotScript,
                new RedisKey[] { countKey },
                new RedisValue[] { maxSlots, currentDbCount });

            return (int)result == 1;
        }
        finally
        {
            var currentLock = await db.StringGetAsync(lockKey);
            if (currentLock == lockToken)
                await db.KeyDeleteAsync(lockKey);
        }
    }

    public async Task ReleaseSlotAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var countKey = $"{SlotCountKeyPrefix}{scheduleId}";
        var value = await db.StringGetAsync(countKey);
        if (!value.IsNullOrEmpty && (long)value > 0)
            await db.StringDecrementAsync(countKey);
    }

    public async Task SyncSlotCountAsync(int scheduleId, int confirmedCount, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var countKey = $"{SlotCountKeyPrefix}{scheduleId}";
        await db.StringSetAsync(countKey, confirmedCount);
    }
}
