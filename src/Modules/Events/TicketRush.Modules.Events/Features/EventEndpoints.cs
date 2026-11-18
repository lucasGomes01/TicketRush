using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using TicketRush.Shared.Auth;
using TicketRush.Shared.Caching;
using TicketRush.Shared.Http;

namespace TicketRush.Modules.Events.Features;

internal sealed record CreateEventRequest(
    string Title, string Description, string Category, string Venue, string City, DateTime StartsAt,
    IReadOnlyList<CreateSectionRequest> Sections);

internal sealed record CreateSectionRequest(string Code, string Name, decimal Price, int Rows, int SeatsPerRow);

internal static partial class EventEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events").WithTags("Events");

        group.MapGet("/", async (EventsRepository repository, CancellationToken ct) =>
                TypedResults.Ok(await repository.ListUpcomingAsync(ct) ?? []))
            .WithSummary("Lista os próximos eventos (cache Redis).");

        group.MapGet("/{id:guid}", async (Guid id, EventsRepository repository, CancellationToken ct) =>
                await repository.GetAsync(id, ct) is { } details
                    ? Results.Ok(details)
                    : Problems.NotFound("Evento não encontrado."))
            .WithSummary("Detalhes do evento e mapa de seções (cache Redis).");

        group.MapPost("/", CreateAsync)
            .RequireAuthorization(AuthExtensions.AdminPolicy)
            .WithSummary("Cria um evento (somente admin) e invalida o cache do catálogo.");
    }

    private static async Task<IResult> CreateAsync(
        CreateEventRequest request, NpgsqlDataSource dataSource, ICacheService cache, TimeProvider clock, CancellationToken ct)
    {
        var errors = Validate(request, clock.GetUtcNow().UtcDateTime);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var id = Guid.CreateVersion7();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        await connection.ExecuteAsync(
            """
            INSERT INTO events.events (id, title, description, category, venue, city, starts_at)
            VALUES (@id, @Title, @Description, @Category, @Venue, @City, @StartsAt)
            """,
            new { id, request.Title, request.Description, request.Category, request.Venue, request.City,
                  StartsAt = request.StartsAt.ToUniversalTime() },
            transaction);

        await connection.ExecuteAsync(
            """
            INSERT INTO events.sections (id, event_id, code, name, price, rows, seats_per_row, sort_order)
            VALUES (@Id, @EventId, @Code, @Name, @Price, @Rows, @SeatsPerRow, @SortOrder)
            """,
            request.Sections.Select((s, i) => new
            {
                Id = Guid.CreateVersion7(), EventId = id, s.Code, s.Name, s.Price, s.Rows, s.SeatsPerRow, SortOrder = i
            }),
            transaction);

        await transaction.CommitAsync(ct);
        await cache.RemoveAsync(EventsRepository.ListCacheKey);

        return Results.Created($"/api/events/{id}", new { id });
    }

    private static Dictionary<string, string[]> Validate(CreateEventRequest request, DateTime now)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string key, string message) => errors[key] = [message];

        if (string.IsNullOrWhiteSpace(request.Title)) Add(nameof(request.Title), "Título é obrigatório.");
        if (string.IsNullOrWhiteSpace(request.Venue)) Add(nameof(request.Venue), "Local é obrigatório.");
        if (string.IsNullOrWhiteSpace(request.City)) Add(nameof(request.City), "Cidade é obrigatória.");
        if (request.StartsAt.ToUniversalTime() <= now) Add(nameof(request.StartsAt), "O evento precisa ser no futuro.");
        if (request.Sections is not { Count: > 0 }) Add(nameof(request.Sections), "Informe ao menos uma seção.");
        else
        {
            if (request.Sections.Any(s => !SectionCodeRegex().IsMatch(s.Code ?? "")))
                Add("Sections.Code", "Código da seção deve ter de 2 a 6 caracteres [A-Z0-9].");
            if (request.Sections.GroupBy(s => s.Code).Any(g => g.Count() > 1))
                Add("Sections.Code", "Códigos de seção devem ser únicos.");
            if (request.Sections.Any(s => s.Rows is < 1 or > 26 || s.SeatsPerRow is < 1 or > 60))
                Add("Sections.Size", "Fileiras entre 1 e 26 e assentos por fileira entre 1 e 60.");
            if (request.Sections.Any(s => s.Price < 0))
                Add("Sections.Price", "Preço não pode ser negativo.");
        }

        return errors;
    }

    [GeneratedRegex("^[A-Z0-9]{2,6}$")]
    private static partial Regex SectionCodeRegex();
}
