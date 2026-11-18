using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using TicketRush.Modules.Events.Contracts;
using TicketRush.Shared.Http;

namespace TicketRush.Modules.Ticketing.Holds;

internal sealed record CreateHoldRequest(Guid EventId, IReadOnlyList<string> SeatIds);

internal sealed record HoldResponse(Guid HoldId, Guid EventId, IReadOnlyList<HeldSeat> Seats, decimal Total, DateTime ExpiresAt);

internal sealed record HeldSeat(string SeatId, string SectionName, decimal Price);

internal sealed record SeatMapResponse(Guid EventId, IReadOnlyList<string> Sold, IReadOnlyList<string> Held);

internal static class HoldEndpoints
{
    public const int MaxSeatsPerHold = 6;

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events/{eventId:guid}/seats", GetSeatMapAsync)
            .WithTags("Ticketing")
            .WithSummary("Assentos vendidos (PostgreSQL) e reservados no momento (Redis).");

        var holds = app.MapGroup("/api/holds").WithTags("Ticketing").RequireAuthorization();

        holds.MapPost("/", CreateAsync)
            .RequireRedisRateLimit("Holds", defaultPermitLimit: 10, defaultWindowSeconds: 10)
            .WithSummary("Reserva assentos por alguns minutos (all-or-nothing, atômico no Redis).");

        holds.MapGet("/{holdId:guid}", GetAsync).WithSummary("Consulta uma reserva.");
        holds.MapDelete("/{holdId:guid}", ReleaseAsync).WithSummary("Libera uma reserva.");
    }

    private static async Task<IResult> GetSeatMapAsync(
        Guid eventId, IEventsModuleApi events, SeatHoldStore store, NpgsqlDataSource dataSource, CancellationToken ct)
    {
        var snapshot = await events.GetEventAsync(eventId, ct);
        if (snapshot is null)
        {
            return Problems.NotFound("Evento não encontrado.");
        }

        var allSeats = snapshot.Sections
            .SelectMany(s => Enumerable.Range(0, s.Rows)
                .SelectMany(row => Enumerable.Range(1, s.SeatsPerRow).Select(n => SeatCode.Format(s.Code, row, n))))
            .ToList();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var sold = (await connection.QueryAsync<string>(
            "SELECT seat_id FROM ticketing.tickets WHERE event_id = @eventId", new { eventId })).ToHashSet();

        var held = await store.GetHeldSeatsAsync(eventId, allSeats);
        return Results.Ok(new SeatMapResponse(eventId, [.. sold], [.. held.Where(s => !sold.Contains(s))]));
    }

    private static async Task<IResult> CreateAsync(
        CreateHoldRequest request,
        HttpContext http,
        IEventsModuleApi events,
        SeatHoldStore store,
        NpgsqlDataSource dataSource,
        IConfiguration configuration,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (http.GetClientId() is not { } clientId)
        {
            return ClientId.Missing();
        }

        var seatIds = (request.SeatIds ?? []).Select(s => s.Trim().ToUpperInvariant()).ToList();
        if (seatIds.Count is 0 or > MaxSeatsPerHold || seatIds.Distinct().Count() != seatIds.Count)
        {
            return Problems.BadRequest($"Selecione de 1 a {MaxSeatsPerHold} assentos diferentes.");
        }

        var snapshot = await events.GetEventAsync(request.EventId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        if (snapshot is null || snapshot.StartsAt <= now)
        {
            return Problems.NotFound("Evento não encontrado ou já realizado.");
        }

        var seats = seatIds.Select(id => (Id: id, Section: snapshot.FindSection(id))).ToList();
        if (seats.FirstOrDefault(s => s.Section is null) is { Id: not null } invalid)
        {
            return Problems.BadRequest($"Assento inválido: {invalid.Id}");
        }

        // Assentos já vendidos ficam no banco (fonte da verdade); o Redis só guarda reservas temporárias.
        await using (var connection = await dataSource.OpenConnectionAsync(ct))
        {
            var alreadySold = (await connection.QueryAsync<string>(
                "SELECT seat_id FROM ticketing.tickets WHERE event_id = @EventId AND seat_id = ANY(@seatIds)",
                new { request.EventId, seatIds = seatIds.ToArray() })).ToList();

            if (alreadySold.Count > 0)
            {
                return Problems.Conflict("Assento já vendido.", new { seatIds = alreadySold });
            }
        }

        var ttl = TimeSpan.FromMinutes(configuration.GetValue("Ticketing:HoldMinutes", 10));
        var result = await store.TryAcquireAsync(request.EventId, clientId, seatIds, ttl, now);

        return result switch
        {
            HoldResult.Success(var hold) => Results.Created($"/api/holds/{hold.HoldId}", ToResponse(hold, snapshot)),
            HoldResult.SeatUnavailable(var seatId) => Problems.Conflict("Assento reservado por outra pessoa.", new { seatIds = new[] { seatId } }),
            _ => throw new InvalidOperationException()
        };
    }

    private static async Task<IResult> GetAsync(Guid holdId, HttpContext http, SeatHoldStore store, IEventsModuleApi events, CancellationToken ct)
    {
        var hold = await store.GetAsync(holdId);
        if (hold is null || hold.ClientId != http.GetClientId())
        {
            return Problems.NotFound("Reserva não encontrada ou expirada.");
        }

        var snapshot = await events.GetEventAsync(hold.EventId, ct);
        return Results.Ok(ToResponse(hold, snapshot!));
    }

    private static async Task<IResult> ReleaseAsync(Guid holdId, HttpContext http, SeatHoldStore store)
    {
        var hold = await store.GetAsync(holdId);
        if (hold is null || hold.ClientId != http.GetClientId())
        {
            return Problems.NotFound("Reserva não encontrada ou expirada.");
        }

        await store.ReleaseAsync(hold);
        return Results.NoContent();
    }

    private static HoldResponse ToResponse(SeatHold hold, EventSnapshot snapshot)
    {
        var seats = hold.SeatIds
            .Select(id => snapshot.FindSection(id)!)
            .Zip(hold.SeatIds, (section, id) => new HeldSeat(id, section.Name, section.Price))
            .ToList();

        return new HoldResponse(hold.HoldId, hold.EventId, seats, seats.Sum(s => s.Price), hold.ExpiresAt);
    }
}
