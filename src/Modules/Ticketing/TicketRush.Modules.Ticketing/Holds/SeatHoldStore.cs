using System.Text.Json;
using StackExchange.Redis;

namespace TicketRush.Modules.Ticketing.Holds;

internal sealed record SeatHold(Guid HoldId, Guid EventId, string ClientId, IReadOnlyList<string> SeatIds, DateTime ExpiresAt);

internal abstract record HoldResult
{
    internal sealed record Success(SeatHold Hold) : HoldResult;
    internal sealed record SeatUnavailable(string SeatId) : HoldResult;
}

/// <summary>
/// Reserva temporária de assentos no Redis.
///
/// Chaves:
///   hold:seat:{eventId}:{seatId} -> holdId  (TTL) — o "lock" de cada assento
///   hold:{holdId}                -> JSON da reserva (TTL)
///
/// A reserva de vários assentos é feita por um script Lua, que o Redis executa de forma atômica:
/// ou todos os assentos são reservados, ou nenhum. Isso elimina a condição de corrida
/// entre "verificar se está livre" e "reservar" mesmo com milhares de requisições simultâneas.
/// </summary>
internal sealed class SeatHoldStore(IConnectionMultiplexer redis)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // KEYS[1..n] = chaves dos assentos, KEYS[n+1] = chave da reserva
    // ARGV[1] = holdId, ARGV[2] = ttl em ms, ARGV[3] = json, ARGV[4..] = seatIds
    private const string AcquireScript =
        """
        local n = #KEYS - 1
        for i = 1, n do
          if redis.call('EXISTS', KEYS[i]) == 1 then
            return ARGV[3 + i]
          end
        end
        for i = 1, n do
          redis.call('SET', KEYS[i], ARGV[1], 'PX', ARGV[2])
        end
        redis.call('SET', KEYS[n + 1], ARGV[3], 'PX', ARGV[2])
        return ''
        """;

    // Só apaga os assentos que ainda pertencem a esta reserva.
    private const string ReleaseScript =
        """
        local n = #KEYS - 1
        for i = 1, n do
          if redis.call('GET', KEYS[i]) == ARGV[1] then
            redis.call('DEL', KEYS[i])
          end
        end
        redis.call('DEL', KEYS[n + 1])
        return 1
        """;

    private const string ExtendScript =
        """
        local n = #KEYS - 1
        if redis.call('EXISTS', KEYS[n + 1]) == 0 then return 0 end
        for i = 1, n do
          if redis.call('GET', KEYS[i]) ~= ARGV[1] then return 0 end
        end
        for i = 1, n do
          redis.call('PEXPIRE', KEYS[i], ARGV[2])
        end
        redis.call('SET', KEYS[n + 1], ARGV[3], 'PX', ARGV[2])
        return 1
        """;

    private IDatabase Db => redis.GetDatabase();

    private static RedisKey SeatKey(Guid eventId, string seatId) => $"hold:seat:{eventId}:{seatId}";
    private static RedisKey HoldKey(Guid holdId) => $"hold:{holdId}";

    private static RedisKey[] Keys(SeatHold hold) =>
        [.. hold.SeatIds.Select(s => SeatKey(hold.EventId, s)), HoldKey(hold.HoldId)];

    public async Task<HoldResult> TryAcquireAsync(Guid eventId, string clientId, IReadOnlyList<string> seatIds, TimeSpan ttl, DateTime now)
    {
        var hold = new SeatHold(Guid.CreateVersion7(), eventId, clientId, seatIds, now.Add(ttl));
        RedisValue[] args =
        [
            hold.HoldId.ToString(),
            (long)ttl.TotalMilliseconds,
            JsonSerializer.Serialize(hold, JsonOptions),
            .. seatIds.Select(s => (RedisValue)s)
        ];

        var result = (string?)await Db.ScriptEvaluateAsync(AcquireScript, Keys(hold), args);
        return string.IsNullOrEmpty(result)
            ? new HoldResult.Success(hold)
            : new HoldResult.SeatUnavailable(result);
    }

    public async Task<SeatHold?> GetAsync(Guid holdId)
    {
        var json = await Db.StringGetAsync(HoldKey(holdId));
        return json.HasValue ? JsonSerializer.Deserialize<SeatHold>(json.ToString(), JsonOptions) : null;
    }

    public async Task<bool> ExtendAsync(Guid holdId, TimeSpan ttl, DateTime now)
    {
        var hold = await GetAsync(holdId);
        if (hold is null)
        {
            return false;
        }

        var extended = hold with { ExpiresAt = now.Add(ttl) };
        var result = await Db.ScriptEvaluateAsync(ExtendScript, Keys(extended),
            [hold.HoldId.ToString(), (long)ttl.TotalMilliseconds, JsonSerializer.Serialize(extended, JsonOptions)]);
        return (int)result == 1;
    }

    public Task ReleaseAsync(SeatHold hold) =>
        Db.ScriptEvaluateAsync(ReleaseScript, Keys(hold), [hold.HoldId.ToString()]);

    /// <summary>Retorna quais assentos do evento estão reservados agora (um único MGET).</summary>
    public async Task<IReadOnlyList<string>> GetHeldSeatsAsync(Guid eventId, IReadOnlyList<string> allSeatIds)
    {
        if (allSeatIds.Count == 0)
        {
            return [];
        }

        var values = await Db.StringGetAsync(allSeatIds.Select(s => SeatKey(eventId, s)).ToArray());
        return allSeatIds.Where((_, i) => values[i].HasValue).ToList();
    }
}
