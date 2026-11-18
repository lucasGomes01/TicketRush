namespace TicketRush.Modules.Orders.Domain;

internal enum OrderStatus
{
    /// <summary>Criado a partir de uma reserva, aguardando pagamento.</summary>
    Pending,
    /// <summary>Pagamento aprovado, aguardando emissão dos ingressos.</summary>
    Paid,
    /// <summary>Ingressos emitidos.</summary>
    Completed,
    /// <summary>Pago, mas a emissão falhou: valor estornado.</summary>
    Refunded,
    /// <summary>A reserva expirou antes do pagamento.</summary>
    Expired
}

internal sealed record OrderItem(string SeatId, string SectionName, decimal Price);

internal sealed class OrderDomainException(string message) : Exception(message);

/// <summary>Agregado Pedido: concentra as regras de transição de status.</summary>
internal sealed class Order
{
    private readonly List<OrderItem> _items = [];

    public Guid Id { get; private set; }
    public Guid HoldId { get; private set; }
    public Guid EventId { get; private set; }
    public string EventTitle { get; private set; } = "";
    public string EventVenue { get; private set; } = "";
    public DateTime EventStartsAt { get; private set; }
    public string ClientId { get; private set; } = "";
    public string CustomerName { get; private set; } = "";
    public string CustomerEmail { get; private set; } = "";
    public OrderStatus Status { get; private set; }
    public decimal Total { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items;

    private Order() { }

    public static Order Create(
        Guid holdId, Guid eventId, string eventTitle, string eventVenue, DateTime eventStartsAt,
        string clientId, string customerName, string customerEmail, IEnumerable<OrderItem> items, DateTime now)
    {
        var order = new Order
        {
            Id = Guid.CreateVersion7(),
            HoldId = holdId,
            EventId = eventId,
            EventTitle = eventTitle,
            EventVenue = eventVenue,
            EventStartsAt = eventStartsAt,
            ClientId = clientId,
            CustomerName = customerName.Trim(),
            CustomerEmail = customerEmail.Trim().ToLowerInvariant(),
            Status = OrderStatus.Pending,
            CreatedAt = now
        };

        order._items.AddRange(items);
        if (order._items.Count == 0)
        {
            throw new OrderDomainException("O pedido precisa ter ao menos um assento.");
        }

        order.Total = order._items.Sum(i => i.Price);
        return order;
    }

    public void MarkPaid(DateTime now)
    {
        EnsureStatus(OrderStatus.Pending);
        Status = OrderStatus.Paid;
        PaidAt = now;
    }

    public void Complete(DateTime now)
    {
        EnsureStatus(OrderStatus.Paid);
        Status = OrderStatus.Completed;
        CompletedAt = now;
    }

    public void Refund(string reason, DateTime now)
    {
        EnsureStatus(OrderStatus.Paid);
        Status = OrderStatus.Refunded;
        FailureReason = reason;
        CompletedAt = now;
    }

    public void Expire()
    {
        EnsureStatus(OrderStatus.Pending);
        Status = OrderStatus.Expired;
        FailureReason = "A reserva expirou antes do pagamento.";
    }

    internal void LoadItems(IEnumerable<OrderItem> items) => _items.AddRange(items);

    private void EnsureStatus(OrderStatus expected)
    {
        if (Status != expected)
        {
            throw new OrderDomainException($"Operação inválida para pedido com status {Status}.");
        }
    }
}
