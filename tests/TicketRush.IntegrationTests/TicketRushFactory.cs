using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace TicketRush.IntegrationTests;

/// <summary>Sobe PostgreSQL e Redis reais em containers (Testcontainers) e a API em memória.</summary>
public sealed class TicketRushFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ticketrush")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        // O teste de concorrência dispara centenas de reservas: desliga o rate limit aqui.
        builder.UseSetting("RateLimiting:Holds:PermitLimit", "100000");
    }

    public HttpClient CreateClientFor(string clientId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Client-Id", clientId);
        return client;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }
}
