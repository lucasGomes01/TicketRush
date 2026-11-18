using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketRush.Modules.Events.Contracts;
using TicketRush.Modules.Events.Features;
using TicketRush.Shared.Modules;

namespace TicketRush.Modules.Events;

/// <summary>Catálogo: eventos, locais e seções (preço e tamanho de cada setor).</summary>
public sealed class EventsModule : IModule
{
    public string Name => "Events";
    public string Schema => "events";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<EventsRepository>();
        services.AddScoped<IEventsModuleApi, EventsModuleApi>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => EventEndpoints.Map(app);
}

internal sealed class EventsModuleApi(EventsRepository repository) : IEventsModuleApi
{
    public async Task<EventSnapshot?> GetEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var details = await repository.GetAsync(eventId, cancellationToken);
        return details is null
            ? null
            : new EventSnapshot(
                details.Id, details.Title, details.Venue, details.City, details.StartsAt,
                details.Sections.Select(s => new SectionSnapshot(s.Code, s.Name, s.Price, s.Rows, s.SeatsPerRow)).ToList());
    }
}
