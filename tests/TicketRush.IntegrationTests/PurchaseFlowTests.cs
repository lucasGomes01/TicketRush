using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TicketRush.IntegrationTests;

public class PurchaseFlowTests(TicketRushFactory factory) : IClassFixture<TicketRushFactory>
{
    // Evento "Pré-estreia Secreta" do seed: 10 lugares (SAL-A1..A5, SAL-B1..B5).
    private const string SmallEventId = "0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a05";
    private const string RockEventId = "0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a01";

    [Fact]
    public async Task Concurrent_holds_on_the_same_seat_should_have_exactly_one_winner()
    {
        const int buyers = 200;
        var clients = Enumerable.Range(0, buyers).Select(i => factory.CreateClientFor($"buyer-{i}")).ToList();
        using var start = new ManualResetEventSlim();

        var attempts = clients.Select(client => Task.Run(async () =>
        {
            start.Wait();
            return await client.PostAsJsonAsync("/api/holds", new { eventId = SmallEventId, seatIds = new[] { "SAL-A3" } });
        })).ToList();

        start.Set();
        var responses = await Task.WhenAll(attempts);

        var unexpected = responses.Where(r => r.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.Conflict)).ToList();
        Assert.True(unexpected.Count == 0,
            string.Join("\n", await Task.WhenAll(unexpected.Select(async r => $"{(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}"))));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(buyers - 1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Hold_is_all_or_nothing()
    {
        var alice = factory.CreateClientFor("alice");
        var bob = factory.CreateClientFor("bob");

        var first = await alice.PostAsJsonAsync("/api/holds", new { eventId = SmallEventId, seatIds = new[] { "SAL-B2" } });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Bob tenta B1+B2: B2 está com Alice, então B1 também NÃO pode ficar reservado.
        var second = await bob.PostAsJsonAsync("/api/holds", new { eventId = SmallEventId, seatIds = new[] { "SAL-B1", "SAL-B2" } });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var seatMap = await bob.GetFromJsonAsync<JsonElement>($"/api/events/{SmallEventId}/seats");
        var held = seatMap.GetProperty("held").EnumerateArray().Select(s => s.GetString()).ToList();
        Assert.Contains("SAL-B2", held);
        Assert.DoesNotContain("SAL-B1", held);
    }

    [Fact]
    public async Task Full_purchase_issues_tickets_through_the_outbox()
    {
        var client = factory.CreateClientFor("carol");

        var hold = await (await client.PostAsJsonAsync("/api/holds", new { eventId = RockEventId, seatIds = new[] { "VIP-C1", "VIP-C2" } }))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JsonElement>();
        var holdId = hold.GetProperty("holdId").GetString();
        Assert.Equal(960m, hold.GetProperty("total").GetDecimal());

        var order = await (await client.PostAsJsonAsync("/api/orders", new { holdId, customerName = "Carol Teste", customerEmail = "carol@example.com" }))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JsonElement>();
        var orderId = order.GetProperty("id").GetString();
        Assert.Equal("Pending", order.GetProperty("status").GetString());

        var pay = await client.PostAsync($"/api/orders/{orderId}/pay", null);
        Assert.Equal(HttpStatusCode.Accepted, pay.StatusCode);

        // A emissão é assíncrona (Outbox -> OrderPaid -> Ticketing -> TicketsIssued -> Orders).
        JsonElement completed = default;
        for (var i = 0; i < 40; i++)
        {
            completed = await client.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}");
            if (completed.GetProperty("status").GetString() == "Completed") break;
            await Task.Delay(250);
        }

        Assert.Equal("Completed", completed.GetProperty("status").GetString());
        Assert.Equal(2, completed.GetProperty("tickets").GetArrayLength());

        var seatMap = await client.GetFromJsonAsync<JsonElement>($"/api/events/{RockEventId}/seats");
        var sold = seatMap.GetProperty("sold").EnumerateArray().Select(s => s.GetString()).ToList();
        Assert.Contains("VIP-C1", sold);
        Assert.Contains("VIP-C2", sold);

        // Assento vendido não pode ser reservado de novo.
        var again = await factory.CreateClientFor("dave")
            .PostAsJsonAsync("/api/holds", new { eventId = RockEventId, seatIds = new[] { "VIP-C1" } });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Another_client_cannot_checkout_someone_elses_hold()
    {
        var owner = factory.CreateClientFor("erin");
        var hold = await (await owner.PostAsJsonAsync("/api/holds", new { eventId = RockEventId, seatIds = new[] { "PLA-A1" } }))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JsonElement>();

        var intruder = factory.CreateClientFor("mallory");
        var response = await intruder.PostAsJsonAsync("/api/orders",
            new { holdId = hold.GetProperty("holdId").GetString(), customerName = "Mallory", customerEmail = "m@example.com" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("SAL-Z1")]   // fileira inexistente
    [InlineData("SAL-A6")]   // número fora da seção
    [InlineData("SAL-A01")]  // formato não canônico
    [InlineData("XYZ-A1")]   // seção inexistente
    public async Task Invalid_seats_are_rejected(string seatId)
    {
        var client = factory.CreateClientFor("frank");
        var response = await client.PostAsJsonAsync("/api/holds", new { eventId = SmallEventId, seatIds = new[] { seatId } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
