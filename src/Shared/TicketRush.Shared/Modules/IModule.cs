using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TicketRush.Shared.Modules;

/// <summary>
/// Contrato que todo módulo implementa. O Bootstrapper só conhece esta interface:
/// cada módulo registra seus próprios serviços, endpoints e migrações (schema próprio).
/// </summary>
public interface IModule
{
    string Name { get; }

    /// <summary>Schema do PostgreSQL que pertence exclusivamente a este módulo.</summary>
    string Schema { get; }

    void Register(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder app);
}
