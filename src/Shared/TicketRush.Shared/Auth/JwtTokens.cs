using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace TicketRush.Shared.Auth;

/// <summary>
/// Emite e valida os JWT da aplicação. A chave vem de Auth:SigningKey; se não estiver configurada,
/// uma chave aleatória é gerada na inicialização (os tokens deixam de valer quando a API reinicia).
/// </summary>
public sealed class JwtTokens
{
    public const string AdminRole = "admin";
    private const string Issuer = "ticketrush";
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    private readonly SymmetricSecurityKey _key;
    private readonly TimeProvider _clock;

    public JwtTokens(IConfiguration configuration, TimeProvider clock, ILogger<JwtTokens> logger)
    {
        _clock = clock;
        var configured = configuration["Auth:SigningKey"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            logger.LogWarning("Auth:SigningKey não configurada; usando uma chave temporária gerada agora.");
            _key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        }
        else
        {
            var bytes = Encoding.UTF8.GetBytes(configured);
            if (bytes.Length < 32)
            {
                throw new InvalidOperationException("Auth:SigningKey precisa ter pelo menos 32 bytes.");
            }

            _key = new SymmetricSecurityKey(bytes);
        }
    }

    public TokenValidationParameters ValidationParameters => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Issuer,
        IssuerSigningKey = _key,
        NameClaimType = "name",
        RoleClaimType = "role",
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    public (string Token, DateTime ExpiresAt) Issue(string userId, string name, string email, bool isAdmin)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new("name", name),
            new(JwtRegisteredClaimNames.Email, email)
        };
        if (isAdmin)
        {
            claims.Add(new Claim("role", AdminRole));
        }

        var expiresAt = now.Add(Lifetime);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Issuer,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256)
        });

        return (token, expiresAt);
    }
}

public static class AuthExtensions
{
    public const string AdminPolicy = "admin";

    public static IServiceCollection AddJwtAuth(this IServiceCollection services)
    {
        services.AddSingleton<JwtTokens>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => options.MapInboundClaims = false);
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtTokens>((options, tokens) => options.TokenValidationParameters = tokens.ValidationParameters);
        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy => policy.RequireRole(JwtTokens.AdminRole));
        return services;
    }

    /// <summary>Id do usuário autenticado (claim "sub").</summary>
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
}
