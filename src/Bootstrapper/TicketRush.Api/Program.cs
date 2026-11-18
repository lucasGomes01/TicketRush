using System.Text.Json.Serialization;
using Npgsql;
using Scalar.AspNetCore;
using StackExchange.Redis;
using TicketRush.Modules.Events;
using TicketRush.Modules.Identity;
using TicketRush.Modules.Orders;
using TicketRush.Modules.Ticketing;
using TicketRush.Shared.Modules;

var builder = WebApplication.CreateBuilder(args);

// O monolito é só a composição dos módulos. Cada módulo é dono dos seus endpoints, serviços e schema.
IModule[] modules = [new IdentityModule(), new EventsModule(), new TicketingModule(), new OrdersModule()];
builder.Services.AddModules(builder.Configuration, modules);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

await app.MigrateModulesAsync();

app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("TicketRush API"));
app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.MapGet("/health", async (NpgsqlDataSource db, IConnectionMultiplexer redis) =>
{
    await using var command = db.CreateCommand("SELECT 1");
    await command.ExecuteScalarAsync();
    var redisLatency = await redis.GetDatabase().PingAsync();
    return Results.Ok(new { status = "healthy", redisLatencyMs = redisLatency.TotalMilliseconds });
}).WithTags("Health");

app.MapModules();

app.Run();

public partial class Program;
