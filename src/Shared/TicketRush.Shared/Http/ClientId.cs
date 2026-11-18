using Microsoft.AspNetCore.Http;

namespace TicketRush.Shared.Http;

/// <summary>
/// Identificador anônimo do comprador. O front gera um UUID e envia no header X-Client-Id.
/// (Num sistema real isso viria do usuário autenticado.)
/// </summary>
public static class ClientId
{
    public const string HeaderName = "X-Client-Id";

    public static string? GetClientId(this HttpContext context)
    {
        var value = context.Request.Headers[HeaderName].ToString();
        return string.IsNullOrWhiteSpace(value) || value.Length > 64 ? null : value;
    }

    public static IResult Missing() =>
        Problems.BadRequest($"Header {HeaderName} é obrigatório.");
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
