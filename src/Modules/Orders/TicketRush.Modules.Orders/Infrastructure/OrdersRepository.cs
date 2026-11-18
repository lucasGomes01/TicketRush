using System.Data;
using Dapper;
using TicketRush.Modules.Orders.Domain;

namespace TicketRush.Modules.Orders.Infrastructure;

internal sealed record OrderSummary(
    Guid Id, string Status, string EventTitle, string EventVenue, DateTime EventStartsAt, decimal Total, DateTime CreatedAt,
    IReadOnlyList<string> SeatIds);

internal static class OrdersRepository
{
    public static async Task<Order?> GetAsync(this IDbConnection connection, Guid id, IDbTransaction? transaction = null, bool forUpdate = false)
    {
        var order = await connection.QuerySingleOrDefaultAsync<Order>(
            $"SELECT * FROM orders.orders WHERE id = @id {(forUpdate ? "FOR UPDATE" : "")}", new { id }, transaction);
        if (order is null)
        {
            return null;
        }

        var items = await connection.QueryAsync<OrderItem>(
            "SELECT seat_id, section_name, price FROM orders.order_items WHERE order_id = @id ORDER BY seat_id", new { id }, transaction);
        order.LoadItems(items);
        return order;
    }

    public static async Task<IReadOnlyList<OrderSummary>> ListByClientAsync(this IDbConnection connection, string clientId, int limit = 50)
    {
        var orders = (await connection.QueryAsync<OrderSummaryRow>(
            """
            SELECT id, status, event_title, event_venue, event_starts_at, total, created_at
            FROM orders.orders WHERE client_id = @clientId
            ORDER BY created_at DESC LIMIT @limit
            """,
            new { clientId, limit })).ToList();

        var seats = (await connection.QueryAsync<(Guid OrderId, string SeatId)>(
                "SELECT order_id, seat_id FROM orders.order_items WHERE order_id = ANY(@ids) ORDER BY seat_id",
                new { ids = orders.Select(o => o.Id).ToArray() }))
            .ToLookup(x => x.OrderId, x => x.SeatId);

        return orders
            .Select(o => new OrderSummary(o.Id, o.Status, o.EventTitle, o.EventVenue, o.EventStartsAt, o.Total, o.CreatedAt, [.. seats[o.Id]]))
            .ToList();
    }

    private sealed record OrderSummaryRow(Guid Id, string Status, string EventTitle, string EventVenue, DateTime EventStartsAt, decimal Total, DateTime CreatedAt);

    public static Task<Guid?> FindIdByHoldAsync(this IDbConnection connection, Guid holdId) =>
        connection.QuerySingleOrDefaultAsync<Guid?>("SELECT id FROM orders.orders WHERE hold_id = @holdId", new { holdId });

    public static async Task InsertAsync(this IDbConnection connection, Order order, IDbTransaction transaction)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO orders.orders (id, hold_id, event_id, event_title, event_venue, event_starts_at, client_id,
                                       customer_name, customer_email, status, total, created_at)
            VALUES (@Id, @HoldId, @EventId, @EventTitle, @EventVenue, @EventStartsAt, @ClientId,
                    @CustomerName, @CustomerEmail, @Status, @Total, @CreatedAt)
            """,
            new
            {
                order.Id, order.HoldId, order.EventId, order.EventTitle, order.EventVenue, order.EventStartsAt, order.ClientId,
                order.CustomerName, order.CustomerEmail, Status = order.Status.ToString(), order.Total, order.CreatedAt
            },
            transaction);

        await connection.ExecuteAsync(
            "INSERT INTO orders.order_items (order_id, seat_id, section_name, price) VALUES (@OrderId, @SeatId, @SectionName, @Price)",
            order.Items.Select(i => new { OrderId = order.Id, i.SeatId, i.SectionName, i.Price }),
            transaction);
    }

    public static Task UpdateStatusAsync(this IDbConnection connection, Order order, IDbTransaction? transaction = null) =>
        connection.ExecuteAsync(
            """
            UPDATE orders.orders
            SET status = @Status, paid_at = @PaidAt, completed_at = @CompletedAt, failure_reason = @FailureReason
            WHERE id = @Id
            """,
            new { order.Id, Status = order.Status.ToString(), order.PaidAt, order.CompletedAt, order.FailureReason },
            transaction);
}
