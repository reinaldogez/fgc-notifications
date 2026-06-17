using Fcg.Notifications.Application.Abstractions;
using StackExchange.Redis;

namespace Fcg.Notifications.Infrastructure.Idempotency;

public class RedisIdempotencyStore(IConnectionMultiplexer redis) : IIdempotencyStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24); // EX 86400

    public async Task<bool> TryMarkAsync(string messageId, CancellationToken ct)
    {
        IDatabase db = redis.GetDatabase();

        // StringSetAsync + When.NotExists É o SET ... NX EX atômico (sem janela SETNX+EXPIRE).
        return await db.StringSetAsync(
            key: $"notifications:processed:{messageId}",
            value: "1",
            expiry: Ttl,
            when: When.NotExists
        );
    }
}
