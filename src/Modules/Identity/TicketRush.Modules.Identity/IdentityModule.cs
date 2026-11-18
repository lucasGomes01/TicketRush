using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketRush.Modules.Identity.Features;
using TicketRush.Shared.Modules;

namespace TicketRush.Modules.Identity;

/// <summary>Contas de usuário: cadastro, login e emissão de JWT.</summary>
public sealed class IdentityModule : IModule
{
    public string Name => "Identity";
    public string Schema => "identity";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Hash PBKDF2 (HMAC-SHA512, 100 mil iterações) do próprio ASP.NET Core Identity.
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => AuthEndpoints.Map(app);
}
