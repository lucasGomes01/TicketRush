using TicketRush.Shared.Messaging;

namespace TicketRush.Modules.Orders.Contracts;

public sealed record OrderPaidIntegrationEvent(
    Guid OrderId,
    Guid HoldId,
    Guid EventId,
    string CustomerName,
    string CustomerEmail,
    IReadOnlyList<PaidSeat> Seats) : IntegrationEvent;

public sealed record PaidSeat(string SeatId, decimal Price);
