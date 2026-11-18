using System.Net.Mail;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using TicketRush.Modules.Events.Contracts;
using TicketRush.Modules.Orders.Contracts;
using TicketRush.Modules.Orders.Domain;
using TicketRush.Modules.Orders.Infrastructure;
using TicketRush.Modules.Ticketing.Contracts;
using TicketRush.Shared.Database;
using TicketRush.Shared.Http;
using TicketRush.Shared.Messaging;

namespace TicketRush.Modules.Orders.Features;

internal sealed record CreateOrderRequest(Guid HoldId, string CustomerName, string CustomerEmail);

internal sealed record OrderResponse(
    Guid Id,
    string Status,
    Guid EventId,
    string EventTitle,
    string EventVenue,
    DateTime EventStartsAt,
    string CustomerName,
    string CustomerEmail,
    decimal Total,
    IReadOnlyList<OrderItem> Items,
    IReadOnlyList<TicketSnapshot> Tickets,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? CompletedAt,
    string? FailureReason,
    DateTime? HoldExpiresAt);

internal static class OrderEndpoints
{
    /// <summary>Tempo extra dado à reserva quando o pagamento é aprovado, para cobrir a emissão assíncrona.</summary>
    private static readonly TimeSpan PaymentHoldExtension = TimeSpan.FromMinutes(15);

    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders").RequireAuthorization();

        group.MapGet("/", ListMineAsync).WithSummary("Pedidos do usuário logado (mais recentes primeiro).");
        group.MapPost("/", CreateAsync).WithSummary("Cria um pedido a partir de uma reserva válida.");
        group.MapPost("/{id:guid}/pay", PayAsync).WithSummary("Simula o pagamento e publica OrderPaid via outbox.");
        group.MapGet("/{id:guid}", GetAsync).WithSummary("Status do pedido e ingressos emitidos.");
    }

    private static async Task<IResult> CreateAsync(
        CreateOrderRequest request,
        HttpContext http,
        ITicketingModuleApi ticketing,
        IEventsModuleApi events,
        NpgsqlDataSource dataSource,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (http.GetClientId() is not { } clientId)
        {
            return ClientId.Missing();
        }

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.CustomerName) || request.CustomerName.Trim().Length < 3)
            errors[nameof(request.CustomerName)] = ["Informe o nome completo."];
        if (!MailAddress.TryCreate(request.CustomerEmail?.Trim(), out _))
            errors[nameof(request.CustomerEmail)] = ["E-mail inválido."];
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        await using var connection = await dataSource.OpenConnectionAsync(ct);

        // Idempotência: repetir o checkout da mesma reserva devolve o mesmo pedido.
        if (await connection.FindIdByHoldAsync(request.HoldId) is { } existingId)
        {
            var existing = await connection.GetAsync(existingId);
            return existing!.ClientId == clientId
                ? Results.Ok(await ToResponseAsync(existing, ticketing, ct))
                : Problems.NotFound("Reserva não encontrada.");
        }

        var hold = await ticketing.GetHoldAsync(request.HoldId, ct);
        if (hold is null || hold.ClientId != clientId)
        {
            return Problems.Conflict("Reserva não encontrada ou expirada. Selecione os assentos novamente.");
        }

        var snapshot = await events.GetEventAsync(hold.EventId, ct);
        if (snapshot is null)
        {
            return Problems.NotFound("Evento não encontrado.");
        }

        var items = hold.SeatIds.Select(seatId =>
        {
            var section = snapshot.FindSection(seatId)!;
            return new OrderItem(seatId, section.Name, section.Price);
        });

        var order = Order.Create(hold.HoldId, snapshot.Id, snapshot.Title, $"{snapshot.Venue} · {snapshot.City}", snapshot.StartsAt,
            clientId, request.CustomerName, request.CustomerEmail!, items, clock.GetUtcNow().UtcDateTime);

        try
        {
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await connection.InsertAsync(order, transaction);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when (ex.IsUniqueViolation())
        {
            // Duas requisições simultâneas com a mesma reserva: a segunda devolve o pedido criado pela primeira.
            var existingId2 = await connection.FindIdByHoldAsync(request.HoldId);
            order = (await connection.GetAsync(existingId2!.Value))!;
            return Results.Ok(await ToResponseAsync(order, ticketing, ct));
        }

        return Results.Created($"/api/orders/{order.Id}", await ToResponseAsync(order, ticketing, ct));
    }

    private static async Task<IResult> PayAsync(
        Guid id, HttpContext http, ITicketingModuleApi ticketing, NpgsqlDataSource dataSource, TimeProvider clock, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var order = await connection.GetAsync(id, transaction, forUpdate: true);
        if (order is null || order.ClientId != http.GetClientId())
        {
            return Problems.NotFound("Pedido não encontrado.");
        }

        if (order.Status is OrderStatus.Paid or OrderStatus.Completed)
        {
            return Results.Accepted($"/api/orders/{id}", await ToResponseAsync(order, ticketing, ct));
        }

        if (order.Status is not OrderStatus.Pending)
        {
            return Problems.Conflict($"Pedido não pode ser pago (status {order.Status}).");
        }

        // A reserva ainda precisa estar ativa. Estendemos para não expirar enquanto os ingressos são emitidos.
        if (!await ticketing.ExtendHoldAsync(order.HoldId, PaymentHoldExtension, ct))
        {
            order.Expire();
            await connection.UpdateStatusAsync(order, transaction);
            await transaction.CommitAsync(ct);
            return Problems.Conflict("A reserva expirou. Selecione os assentos novamente.");
        }

        // Pagamento simulado (aprovado). Status + evento gravados na MESMA transação (outbox).
        order.MarkPaid(clock.GetUtcNow().UtcDateTime);
        await connection.UpdateStatusAsync(order, transaction);
        await connection.AddToOutboxAsync(OrdersModule.SchemaName,
            new OrderPaidIntegrationEvent(order.Id, order.HoldId, order.EventId, order.CustomerName, order.CustomerEmail,
                order.Items.Select(i => new PaidSeat(i.SeatId, i.Price)).ToList()),
            transaction);
        await transaction.CommitAsync(ct);

        return Results.Accepted($"/api/orders/{id}", await ToResponseAsync(order, ticketing, ct));
    }

    private static async Task<IResult> ListMineAsync(HttpContext http, NpgsqlDataSource dataSource, CancellationToken ct)
    {
        if (http.GetClientId() is not { } clientId)
        {
            return ClientId.Missing();
        }

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return Results.Ok(await connection.ListByClientAsync(clientId));
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext http, ITicketingModuleApi ticketing, NpgsqlDataSource dataSource, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var order = await connection.GetAsync(id);
        return order is null || order.ClientId != http.GetClientId()
            ? Problems.NotFound("Pedido não encontrado.")
            : Results.Ok(await ToResponseAsync(order, ticketing, ct));
    }

    private static async Task<OrderResponse> ToResponseAsync(Order order, ITicketingModuleApi ticketing, CancellationToken ct)
    {
        var tickets = order.Status == OrderStatus.Completed
            ? await ticketing.GetTicketsByOrderAsync(order.Id, ct)
            : [];

        var holdExpiresAt = order.Status == OrderStatus.Pending
            ? (await ticketing.GetHoldAsync(order.HoldId, ct))?.ExpiresAt
            : null;

        return new OrderResponse(order.Id, order.Status.ToString(), order.EventId, order.EventTitle, order.EventVenue,
            order.EventStartsAt, order.CustomerName, order.CustomerEmail, order.Total, order.Items, tickets,
            order.CreatedAt, order.PaidAt, order.CompletedAt, order.FailureReason, holdExpiresAt);
    }
}
