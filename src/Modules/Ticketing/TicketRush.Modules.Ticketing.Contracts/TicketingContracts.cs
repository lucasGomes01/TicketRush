using TicketRush.Shared.Messaging;

namespace TicketRush.Modules.Ticketing.Contracts;

/// <summary>API pública do módulo Ticketing (reservas temporárias e ingressos emitidos).</summary>
public interface ITicketingModuleApi
{
    Task<SeatHoldSnapshot?> GetHoldAsync(Guid holdId, CancellationToken cancellationToken);

    /// <summary>Estende a reserva (usado no pagamento para a reserva não expirar durante a emissão).</summary>
    Task<bool> ExtendHoldAsync(Guid holdId, TimeSpan ttl, CancellationToken cancellationToken);

    Task<IReadOnlyList<TicketSnapshot>> GetTicketsByOrderAsync(Guid orderId, CancellationToken cancellationToken);
}

public sealed record SeatHoldSnapshot(Guid HoldId, Guid EventId, string ClientId, IReadOnlyList<string> SeatIds, DateTime ExpiresAt);

public sealed record TicketSnapshot(Guid Id, string Code, string SeatId, decimal Price, DateTime IssuedAt);

public sealed record TicketsIssuedIntegrationEvent(Guid OrderId, int Count) : IntegrationEvent;

public sealed record TicketIssuanceFailedIntegrationEvent(Guid OrderId, string Reason) : IntegrationEvent;
