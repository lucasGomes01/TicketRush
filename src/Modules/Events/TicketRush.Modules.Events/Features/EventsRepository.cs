using Dapper;
using Npgsql;
using TicketRush.Shared.Caching;

namespace TicketRush.Modules.Events.Features;

internal sealed record EventSummary(
    Guid Id, string Title, string Category, string Venue, string City, DateTime StartsAt, decimal MinPrice, int TotalSeats);

internal sealed record EventDetails(
    Guid Id, string Title, string Description, string Category, string Venue, string City, DateTime StartsAt,
    IReadOnlyList<SectionDetails> Sections)
{
    public int TotalSeats => Sections.Sum(s => s.Rows * s.SeatsPerRow);
}

internal sealed record SectionDetails(string Code, string Name, decimal Price, int Rows, int SeatsPerRow);

/// <summary>Leituras do catálogo com cache-aside no Redis (o catálogo muda pouco e é muito lido).</summary>
internal sealed class EventsRepository(NpgsqlDataSource dataSource, ICacheService cache)
{
    public const string ListCacheKey = "events:list";
    public static string DetailsCacheKey(Guid id) => $"events:{id}";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public Task<List<EventSummary>?> ListUpcomingAsync(CancellationToken ct) =>
        cache.GetOrCreateAsync(ListCacheKey, async token =>
        {
            await using var connection = await dataSource.OpenConnectionAsync(token);
            var rows = await connection.QueryAsync<EventSummary>(
                """
                SELECT e.id, e.title, e.category, e.venue, e.city, e.starts_at,
                       MIN(s.price) AS min_price,
                       SUM(s.rows * s.seats_per_row)::int AS total_seats
                FROM events.events e
                JOIN events.sections s ON s.event_id = e.id
                WHERE e.starts_at > now()
                GROUP BY e.id
                ORDER BY e.starts_at
                """);
            return rows.ToList();
        }, CacheTtl, ct);

    public Task<EventDetails?> GetAsync(Guid id, CancellationToken ct) =>
        cache.GetOrCreateAsync(DetailsCacheKey(id), async token =>
        {
            await using var connection = await dataSource.OpenConnectionAsync(token);
            await using var grid = await connection.QueryMultipleAsync(
                """
                SELECT id, title, description, category, venue, city, starts_at FROM events.events WHERE id = @id;
                SELECT code, name, price, rows, seats_per_row FROM events.sections WHERE event_id = @id ORDER BY sort_order;
                """,
                new { id });

            var header = await grid.ReadSingleOrDefaultAsync<EventHeader>();
            if (header is null)
            {
                return null;
            }

            var sections = (await grid.ReadAsync<SectionDetails>()).ToList();
            return new EventDetails(header.Id, header.Title, header.Description, header.Category, header.Venue,
                header.City, header.StartsAt, sections);
        }, CacheTtl, ct);

    private sealed record EventHeader(Guid Id, string Title, string Description, string Category, string Venue, string City, DateTime StartsAt);
}
