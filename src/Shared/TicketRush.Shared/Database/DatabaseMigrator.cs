using DbUp;
using DbUp.Engine.Output;
using Microsoft.Extensions.Logging;
using Npgsql;
using TicketRush.Shared.Modules;

namespace TicketRush.Shared.Database;

/// <summary>
/// Executa as migrações de cada módulo com DbUp. Os scripts ficam embutidos no assembly
/// do próprio módulo (pasta Database/Scripts) e o journal é gravado no schema do módulo,
/// então cada módulo versiona seu banco de forma independente.
/// </summary>
public static class DatabaseMigrator
{
    public static async Task EnsureDatabaseAsync(string connectionString, ILogger logger)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var database = builder.Database!;
        builder.Database = "postgres";

        // O banco pode demorar alguns segundos para aceitar conexões quando sobe junto com a API (docker compose).
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(builder.ConnectionString);
                await connection.OpenAsync();
                await using var check = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", connection);
                check.Parameters.AddWithValue("name", database);
                if (await check.ExecuteScalarAsync() is null)
                {
                    await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
                    await create.ExecuteNonQueryAsync();
                    logger.LogInformation("Banco {Database} criado", database);
                }

                return;
            }
            catch (Exception ex) when (ex is NpgsqlException or System.Net.Sockets.SocketException && attempt < 15)
            {
                logger.LogWarning("PostgreSQL indisponível (tentativa {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
    }

    public static void Migrate(string connectionString, IModule module, ILogger logger)
    {
        using (var connection = new NpgsqlConnection(connectionString))
        {
            connection.Open();
            using var command = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS {module.Schema}", connection);
            command.ExecuteNonQuery();
        }

        var assembly = module.GetType().Assembly;
        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(assembly, name => name.Contains(".Database.Scripts.", StringComparison.Ordinal))
            .JournalToPostgresqlTable(module.Schema, "schema_versions")
            .WithTransactionPerScript()
            .LogTo(new DbUpLogger(logger, module.Name))
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException($"Falha ao migrar o módulo {module.Name}", result.Error);
        }
    }

    private sealed class DbUpLogger(ILogger logger, string module) : IUpgradeLog
    {
        public void LogTrace(string format, params object[] args) => logger.LogTrace("[{Module}] {Message}", module, string.Format(format, args));
        public void LogDebug(string format, params object[] args) => logger.LogDebug("[{Module}] {Message}", module, string.Format(format, args));
        public void LogInformation(string format, params object[] args) => logger.LogInformation("[{Module}] {Message}", module, string.Format(format, args));
        public void LogWarning(string format, params object[] args) => logger.LogWarning("[{Module}] {Message}", module, string.Format(format, args));
        public void LogError(string format, params object[] args) => logger.LogError("[{Module}] {Message}", module, string.Format(format, args));
        public void LogError(Exception ex, string format, params object[] args) => logger.LogError(ex, "[{Module}] {Message}", module, string.Format(format, args));
    }
}
