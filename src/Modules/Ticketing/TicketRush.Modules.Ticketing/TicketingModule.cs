using Dapper;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TicketRush.Modules.Orders.Contracts;
using TicketRush.Modules.Ticketing.Contracts;
using TicketRush.Modules.Ticketing.Holds;
using TicketRush.Modules.Ticketing.Tickets;
using TicketRush.Shared.Messaging;
using TicketRush.Shared.Modules;

namespace TicketRush.Modules.Ticketing;

/// <summary>Reservas temporárias de assentos (Redis) e emissão de ingressos (PostgreSQL).</summary>
public sealed class TicketingModule : IModule
{
    internal const string SchemaName = "ticketing";

    public string Name => "Ticketing";
    public string Schema => SchemaName;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<SeatHoldStore>();
        services.AddScoped<ITicketingModuleApi, TicketingModuleApi>();
        services.AddScoped<IIntegrationEventHandler<OrderPaidIntegrationEvent>, OrderPaidHandler>();
        services.AddOutbox(SchemaName);
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => HoldEndpoints.Map(app);
}

internal sealed class TicketingModuleApi(SeatHoldStore holds, NpgsqlDataSource dataSource, TimeProvider clock) : ITicketingModuleApi
{
    public async Task<SeatHoldSnapshot?> GetHoldAsync(Guid holdId, CancellationToken cancellationToken) =>
        await holds.GetAsync(holdId) is { } hold
            ? new SeatHoldSnapshot(hold.HoldId, hold.EventId, hold.ClientId, hold.SeatIds, hold.ExpiresAt)
            : null;

    public Task<bool> ExtendHoldAsync(Guid holdId, TimeSpan ttl, CancellationToken cancellationToken) =>
        holds.ExtendAsync(holdId, ttl, clock.GetUtcNow().UtcDateTime);

    public async Task<IReadOnlyList<TicketSnapshot>> GetTicketsByOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var tickets = await connection.QueryAsync<TicketSnapshot>(
            "SELECT id, code, seat_id, price, issued_at FROM ticketing.tickets WHERE order_id = @orderId ORDER BY seat_id",
            new { orderId });
        return tickets.ToList();
    }
}
