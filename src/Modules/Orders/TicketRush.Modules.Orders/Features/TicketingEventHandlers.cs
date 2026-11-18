using Microsoft.Extensions.Logging;
using Npgsql;
using TicketRush.Modules.Orders.Domain;
using TicketRush.Modules.Orders.Infrastructure;
using TicketRush.Modules.Ticketing.Contracts;
using TicketRush.Shared.Messaging;

namespace TicketRush.Modules.Orders.Features;

internal sealed class TicketsIssuedHandler(NpgsqlDataSource dataSource, TimeProvider clock)
    : IIntegrationEventHandler<TicketsIssuedIntegrationEvent>
{
    public async Task HandleAsync(TicketsIssuedIntegrationEvent issued, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var order = await connection.GetAsync(issued.OrderId);
        if (order is not { Status: OrderStatus.Paid })
        {
            return; // já processado
        }

        order.Complete(clock.GetUtcNow().UtcDateTime);
        await connection.UpdateStatusAsync(order);
    }
}

internal sealed class TicketIssuanceFailedHandler(NpgsqlDataSource dataSource, TimeProvider clock, ILogger<TicketIssuanceFailedHandler> logger)
    : IIntegrationEventHandler<TicketIssuanceFailedIntegrationEvent>
{
    public async Task HandleAsync(TicketIssuanceFailedIntegrationEvent failed, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var order = await connection.GetAsync(failed.OrderId);
        if (order is not { Status: OrderStatus.Paid })
        {
            return;
        }

        // Compensação: num sistema real aqui entraria a chamada de estorno no gateway de pagamento.
        order.Refund(failed.Reason, clock.GetUtcNow().UtcDateTime);
        await connection.UpdateStatusAsync(order);
        logger.LogWarning("Pedido {OrderId} estornado: {Reason}", order.Id, failed.Reason);
    }
}
