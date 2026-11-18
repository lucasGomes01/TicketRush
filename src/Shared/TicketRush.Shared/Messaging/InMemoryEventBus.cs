using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TicketRush.Shared.Messaging;

/// <summary>
/// Barramento em processo. Como as mensagens chegam aqui vindas do Outbox (já persistidas),
/// trocar por RabbitMQ/Kafka no futuro não exige mudar os módulos, só esta implementação.
/// </summary>
internal sealed class InMemoryEventBus(IServiceScopeFactory scopeFactory, ILogger<InMemoryEventBus> logger) : IEventBus
{
    public async Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var eventType = integrationEvent.GetType();
        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
        var handle = handlerType.GetMethod("HandleAsync")!;

        await using var scope = scopeFactory.CreateAsyncScope();
        foreach (var handler in scope.ServiceProvider.GetServices(handlerType))
        {
            logger.LogInformation("Entregando {Event} para {Handler}", eventType.Name, handler!.GetType().Name);
            await (Task)handle.Invoke(handler, [integrationEvent, cancellationToken])!;
        }
    }
}
