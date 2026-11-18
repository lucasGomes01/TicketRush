using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;
using TicketRush.Shared.Auth;
using TicketRush.Shared.Caching;
using TicketRush.Shared.Database;
using TicketRush.Shared.Messaging;

namespace TicketRush.Shared.Modules;

public static class ModuleExtensions
{
    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration, IReadOnlyList<IModule> modules)
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        var postgres = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres não configurada.");
        var redis = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis não configurada.");

        services.AddSingleton(_ =>
        {
            var dataSource = new NpgsqlDataSourceBuilder(postgres);
            // O padrão do Npgsql (100) é igual ao max_connections padrão do PostgreSQL: num pico de acessos
            // o banco recusaria conexões ("too many clients"). Com um pool menor, as requisições aguardam a vez.
            dataSource.ConnectionStringBuilder.MaxPoolSize = configuration.GetValue("Database:MaxPoolSize", 40);
            return dataSource.Build();
        });
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redis);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddJwtAuth();

        foreach (var module in modules)
        {
            module.Register(services, configuration);
        }

        services.AddSingleton(modules);
        return services;
    }

    public static async Task MigrateModulesAsync(this WebApplication app)
    {
        var modules = app.Services.GetRequiredService<IReadOnlyList<IModule>>();
        var connectionString = app.Configuration.GetConnectionString("Postgres")!;
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DbUp");

        await DatabaseMigrator.EnsureDatabaseAsync(connectionString, logger);
        foreach (var module in modules)
        {
            DatabaseMigrator.Migrate(connectionString, module, logger);
        }
    }

    public static WebApplication MapModules(this WebApplication app)
    {
        foreach (var module in app.Services.GetRequiredService<IReadOnlyList<IModule>>())
        {
            module.MapEndpoints(app);
        }

        return app;
    }
}
