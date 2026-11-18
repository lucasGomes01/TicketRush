using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace TicketRush.Shared.Messaging;

/// <summary>
/// Transactional Outbox: o evento é gravado na mesma transação que altera o estado do módulo
/// e só depois é publicado. Assim nunca existe pedido pago sem o evento OrderPaid (e vice-versa).
/// Cada módulo tem a sua própria tabela {schema}.outbox_messages.
/// </summary>
public static class Outbox
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task AddToOutboxAsync(this IDbConnection connection, string schema, IIntegrationEvent integrationEvent, IDbTransaction transaction)
    {
        var type = integrationEvent.GetType();
        return connection.ExecuteAsync(
            $"""
             INSERT INTO {schema}.outbox_messages (id, type, content, occurred_at)
             VALUES (@Id, @Type, CAST(@Content AS jsonb), @OccurredAt)
             """,
            new
            {
                integrationEvent.Id,
                Type = $"{type.FullName}, {type.Assembly.GetName().Name}",
                Content = JsonSerializer.Serialize(integrationEvent, type, JsonOptions),
                integrationEvent.OccurredAt
            },
            transaction);
    }

    public static IServiceCollection AddOutbox(this IServiceCollection services, string schema) =>
        services.AddSingleton<IHostedService>(sp => ActivatorUtilities.CreateInstance<OutboxProcessor>(sp, schema));
}

internal sealed class OutboxProcessor(
    string schema,
    NpgsqlDataSource dataSource,
    IEventBus eventBus,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                while (await ProcessBatchAsync(stoppingToken) == BatchSize) { }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Erro processando outbox do schema {Schema}", schema);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // SKIP LOCKED permite várias instâncias da API processando o outbox sem duplicar mensagens.
        var messages = (await connection.QueryAsync<OutboxMessage>(
            $"""
             SELECT id, type, content::text AS content
             FROM {schema}.outbox_messages
             WHERE processed_at IS NULL
             ORDER BY occurred_at
             LIMIT {BatchSize}
             FOR UPDATE SKIP LOCKED
             """,
            transaction: transaction)).ToList();

        foreach (var message in messages)
        {
            string? error = null;
            try
            {
                var type = Type.GetType(message.Type, throwOnError: true)!;
                var integrationEvent = (IIntegrationEvent)JsonSerializer.Deserialize(message.Content, type, Outbox.JsonOptions)!;
                await eventBus.PublishAsync(integrationEvent, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha ao publicar mensagem {MessageId}", message.Id);
                error = ex.ToString();
            }

            await connection.ExecuteAsync(
                $"UPDATE {schema}.outbox_messages SET processed_at = now(), error = @error WHERE id = @id",
                new { id = message.Id, error },
                transaction);
        }

        await transaction.CommitAsync(cancellationToken);
        return messages.Count;
    }

    private sealed record OutboxMessage(Guid Id, string Type, string Content);
}
