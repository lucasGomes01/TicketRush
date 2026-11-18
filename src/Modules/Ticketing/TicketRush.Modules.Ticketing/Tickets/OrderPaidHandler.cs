using System.Security.Cryptography;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using TicketRush.Modules.Orders.Contracts;
using TicketRush.Modules.Ticketing.Contracts;
using TicketRush.Modules.Ticketing.Holds;
using TicketRush.Shared.Database;
using TicketRush.Shared.Messaging;

namespace TicketRush.Modules.Ticketing.Tickets;

/// <summary>
/// Pedido pago => emite os ingressos. Se algum assento já tiver ingresso (a constraint única
/// do banco disparou), publica TicketIssuanceFailed para o módulo Orders estornar o pedido.
/// </summary>
internal sealed class OrderPaidHandler(
    NpgsqlDataSource dataSource,
    SeatHoldStore holds,
    TimeProvider clock,
    ILogger<OrderPaidHandler> logger) : IIntegrationEventHandler<OrderPaidIntegrationEvent>
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task HandleAsync(OrderPaidIntegrationEvent paid, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        // Idempotência: o outbox garante entrega "at least once".
        var alreadyIssued = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM ticketing.tickets WHERE order_id = @OrderId", new { paid.OrderId });
        if (alreadyIssued > 0)
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        try
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await connection.ExecuteAsync(
                """
                INSERT INTO ticketing.tickets (id, code, event_id, seat_id, order_id, holder_name, holder_email, price, issued_at)
                VALUES (@Id, @Code, @EventId, @SeatId, @OrderId, @HolderName, @HolderEmail, @Price, @IssuedAt)
                """,
                paid.Seats.Select(seat => new
                {
                    Id = Guid.CreateVersion7(),
                    Code = NewTicketCode(),
                    paid.EventId,
                    seat.SeatId,
                    paid.OrderId,
                    HolderName = paid.CustomerName,
                    HolderEmail = paid.CustomerEmail,
                    seat.Price,
                    IssuedAt = now
                }),
                transaction);

            await connection.AddToOutboxAsync(TicketingModule.SchemaName,
                new TicketsIssuedIntegrationEvent(paid.OrderId, paid.Seats.Count), transaction);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("{Count} ingressos emitidos para o pedido {OrderId}", paid.Seats.Count, paid.OrderId);
        }
        catch (Exception ex) when (ex.IsUniqueViolation())
        {
            logger.LogWarning("Assento já vendido ao emitir pedido {OrderId}; solicitando estorno", paid.OrderId);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await connection.AddToOutboxAsync(TicketingModule.SchemaName,
                new TicketIssuanceFailedIntegrationEvent(paid.OrderId, "Um dos assentos foi vendido para outra pessoa."), transaction);
            await transaction.CommitAsync(cancellationToken);
        }

        // Os assentos agora estão no banco como vendidos; a reserva temporária não é mais necessária.
        if (await holds.GetAsync(paid.HoldId) is { } hold)
        {
            await holds.ReleaseAsync(hold);
        }
    }

    private static string NewTicketCode() =>
        "TR-" + RandomNumberGenerator.GetString(Alphabet, 8);
}
