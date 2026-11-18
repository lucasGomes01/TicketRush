using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace TicketRush.Shared.Http;

/// <summary>
/// Rate limit de janela fixa compartilhado entre instâncias (INCR + EXPIRE no Redis).
/// Configurável em RateLimiting:{policy}:PermitLimit / WindowSeconds.
/// </summary>
public static class RedisRateLimit
{
    public static TBuilder RequireRedisRateLimit<TBuilder>(this TBuilder builder, string policy, int defaultPermitLimit, int defaultWindowSeconds)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var configuration = http.RequestServices.GetRequiredService<IConfiguration>();
            var limit = configuration.GetValue($"RateLimiting:{policy}:PermitLimit", defaultPermitLimit);
            var window = configuration.GetValue($"RateLimiting:{policy}:WindowSeconds", defaultWindowSeconds);

            var client = http.GetClientId() ?? http.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
            var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / window;
            var key = $"ratelimit:{policy}:{client}:{bucket}";

            var db = http.RequestServices.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
            var count = await db.StringIncrementAsync(key);
            if (count == 1)
            {
                await db.KeyExpireAsync(key, TimeSpan.FromSeconds(window));
            }

            if (count > limit)
            {
                http.Response.Headers.RetryAfter = window.ToString();
                return Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Muitas requisições. Tente novamente em instantes.");
            }

            return await next(context);
        });
}
