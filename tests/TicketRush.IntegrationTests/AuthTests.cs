using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace TicketRush.IntegrationTests;

public class AuthTests(TicketRushFactory factory) : IClassFixture<TicketRushFactory>
{
    private const string SmallEventId = "0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a05";

    [Fact]
    public async Task Register_then_login_returns_a_token_that_works()
    {
        var anonymous = factory.CreateClient();
        var email = $"ana.{Guid.NewGuid():N}@example.com";

        var register = await anonymous.PostAsJsonAsync("/api/auth/register", new { name = "Ana Souza", email, password = "senha-forte-123" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = email.ToUpperInvariant(), password = "senha-forte-123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await authed.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("Ana Souza", me.GetProperty("name").GetString());
        Assert.False(me.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task Duplicate_email_and_wrong_password_are_rejected()
    {
        var client = factory.CreateClient();
        var email = $"bruno.{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new { name = "Bruno Lima", email, password = "senha-forte-123" });

        var duplicate = await client.PostAsJsonAsync("/api/auth/register", new { name = "Outro", email, password = "outra-senha-456" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "senha-errada" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
    }

    [Fact]
    public async Task Weak_password_is_rejected()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { name = "Carla Dias", email = "carla@example.com", password = "123" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Holding_seats_requires_login()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/holds", new { eventId = SmallEventId, seatIds = new[] { "SAL-B5" } });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Only_admins_can_create_events()
    {
        var newEvent = new
        {
            title = "Jazz na Praça", description = "Noite de jazz.", category = "Show", venue = "Praça Central", city = "Curitiba",
            startsAt = DateTime.UtcNow.AddMonths(6),
            sections = new[] { new { code = "PLA", name = "Plateia", price = 80m, rows = 4, seatsPerRow = 10 } }
        };

        var asBuyer = await factory.CreateClientFor("buyer-x").PostAsJsonAsync("/api/events", newEvent);
        Assert.Equal(HttpStatusCode.Forbidden, asBuyer.StatusCode);

        var asAdmin = await factory.CreateClientFor("admin-x", isAdmin: true).PostAsJsonAsync("/api/events", newEvent);
        Assert.Equal(HttpStatusCode.Created, asAdmin.StatusCode);
    }

    [Fact]
    public async Task My_orders_lists_only_my_orders()
    {
        var me = factory.CreateClientFor("lister");
        var hold = await (await me.PostAsJsonAsync("/api/holds", new { eventId = SmallEventId, seatIds = new[] { "SAL-B4" } }))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JsonElement>();
        await (await me.PostAsJsonAsync("/api/orders", new { holdId = hold.GetProperty("holdId").GetString(), customerName = "Lister Teste", customerEmail = "l@example.com" }))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JsonElement>();

        var mine = await me.GetFromJsonAsync<JsonElement>("/api/orders");
        Assert.Equal(1, mine.GetArrayLength());
        Assert.Equal("SAL-B4", mine[0].GetProperty("seatIds")[0].GetString());

        var someoneElse = await factory.CreateClientFor("stranger").GetFromJsonAsync<JsonElement>("/api/orders");
        Assert.Equal(0, someoneElse.GetArrayLength());
    }
}
