using Microsoft.AspNetCore.Http;
using TicketRush.Shared.Auth;

namespace TicketRush.Shared.Http;

/// <summary>
/// Dono das reservas e pedidos: o usuário autenticado (claim "sub" do JWT).
/// </summary>
public static class ClientId
{
    public static string? GetClientId(this HttpContext context) => context.User.GetUserId();

    public static IResult Missing() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Entre na sua conta para continuar.");
}

public static class Problems
{
    public static IResult BadRequest(string title) =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: title);

    public static IResult NotFound(string title) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: title);

    public static IResult Conflict(string title, object? extensions = null) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: title,
            extensions: extensions is null ? null : new Dictionary<string, object?> { ["details"] = extensions });
}
