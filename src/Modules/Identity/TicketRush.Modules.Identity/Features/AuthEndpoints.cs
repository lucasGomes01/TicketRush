using System.Net.Mail;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using TicketRush.Shared.Auth;
using TicketRush.Shared.Database;
using TicketRush.Shared.Http;

namespace TicketRush.Modules.Identity.Features;

internal sealed record RegisterRequest(string Name, string Email, string Password);

internal sealed record LoginRequest(string Email, string Password);

internal sealed record UserResponse(Guid Id, string Name, string Email, bool IsAdmin);

internal sealed record AuthResponse(string Token, DateTime ExpiresAt, UserResponse User);

internal sealed class User
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
    public string PasswordHash { get; set; } = "";
}

internal static class AuthEndpoints
{
    private const int MinPasswordLength = 8;

    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Identity");

        group.MapPost("/register", RegisterAsync)
            .RequireRedisRateLimit("Auth", defaultPermitLimit: 10, defaultWindowSeconds: 60)
            .WithSummary("Cria a conta e já devolve o token.");

        group.MapPost("/login", LoginAsync)
            .RequireRedisRateLimit("Auth", defaultPermitLimit: 10, defaultWindowSeconds: 60)
            .WithSummary("Entra com e-mail e senha (limite de tentativas por IP).");

        group.MapGet("/me", MeAsync)
            .RequireAuthorization()
            .WithSummary("Dados do usuário do token.");
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request, NpgsqlDataSource dataSource, IPasswordHasher<User> hasher, JwtTokens tokens,
        IConfiguration configuration, TimeProvider clock, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? "";
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";

        var errors = new Dictionary<string, string[]>();
        if (name.Length < 3) errors[nameof(request.Name)] = ["Informe seu nome completo."];
        if (!MailAddress.TryCreate(email, out _)) errors[nameof(request.Email)] = ["E-mail inválido."];
        if ((request.Password ?? "").Length < MinPasswordLength)
            errors[nameof(request.Password)] = [$"A senha precisa ter pelo menos {MinPasswordLength} caracteres."];
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var user = new User { Id = Guid.CreateVersion7(), Name = name, Email = email };
        user.PasswordHash = hasher.HashPassword(user, request.Password!);

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await connection.ExecuteAsync(
                "INSERT INTO identity.users (id, name, email, password_hash, created_at) VALUES (@Id, @Name, @Email, @PasswordHash, @CreatedAt)",
                new { user.Id, user.Name, user.Email, user.PasswordHash, CreatedAt = clock.GetUtcNow().UtcDateTime });
        }
        catch (Exception ex) when (ex.IsUniqueViolation())
        {
            return Problems.Conflict("Já existe uma conta com esse e-mail.");
        }

        return Results.Created("/api/auth/me", Authenticate(user, tokens, configuration));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request, NpgsqlDataSource dataSource, IPasswordHasher<User> hasher, JwtTokens tokens,
        IConfiguration configuration, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var user = await connection.QuerySingleOrDefaultAsync<User>(
            "SELECT id, name, email, password_hash FROM identity.users WHERE lower(email) = lower(@email)",
            new { email = request.Email?.Trim() ?? "" });

        var result = user is null
            ? PasswordVerificationResult.Failed
            : hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? "");

        if (result == PasswordVerificationResult.Failed)
        {
            // Mesma mensagem para e-mail inexistente e senha errada: não revela quem tem conta.
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "E-mail ou senha incorretos.");
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user!.PasswordHash = hasher.HashPassword(user, request.Password!);
            await connection.ExecuteAsync("UPDATE identity.users SET password_hash = @PasswordHash WHERE id = @Id", user);
        }

        return Results.Ok(Authenticate(user!, tokens, configuration));
    }

    private static async Task<IResult> MeAsync(HttpContext http, NpgsqlDataSource dataSource, IConfiguration configuration, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.GetUserId(), out var id))
        {
            return ClientId.Missing();
        }

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var user = await connection.QuerySingleOrDefaultAsync<User>(
            "SELECT id, name, email, password_hash FROM identity.users WHERE id = @id", new { id });

        return user is null
            ? ClientId.Missing()
            : Results.Ok(new UserResponse(user.Id, user.Name, user.Email, IsAdmin(user.Email, configuration)));
    }

    private static AuthResponse Authenticate(User user, JwtTokens tokens, IConfiguration configuration)
    {
        var isAdmin = IsAdmin(user.Email, configuration);
        var (token, expiresAt) = tokens.Issue(user.Id.ToString(), user.Name, user.Email, isAdmin);
        return new AuthResponse(token, expiresAt, new UserResponse(user.Id, user.Name, user.Email, isAdmin));
    }

    /// <summary>Administradores são definidos por configuração (Auth:AdminEmails), não por cadastro.</summary>
    private static bool IsAdmin(string email, IConfiguration configuration) =>
        (configuration.GetSection("Auth:AdminEmails").Get<string[]>() ?? [])
            .Contains(email, StringComparer.OrdinalIgnoreCase);
}
