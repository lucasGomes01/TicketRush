using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketRush.Modules.Orders.Features;
using TicketRush.Modules.Ticketing.Contracts;
using TicketRush.Shared.Messaging;
using TicketRush.Shared.Modules;

namespace TicketRush.Modules.Orders;

/// <summary>Checkout: pedido, pagamento (simulado) e acompanhamento até a emissão dos ingressos.</summary>
public sealed class OrdersModule : IModule
{
    internal const string SchemaName = "orders";

    public string Name => "Orders";
    public string Schema => SchemaName;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IIntegrationEventHandler<TicketsIssuedIntegrationEvent>, TicketsIssuedHandler>();
        services.AddScoped<IIntegrationEventHandler<TicketIssuanceFailedIntegrationEvent>, TicketIssuanceFailedHandler>();
        services.AddOutbox(SchemaName);
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => OrderEndpoints.Map(app);
}
