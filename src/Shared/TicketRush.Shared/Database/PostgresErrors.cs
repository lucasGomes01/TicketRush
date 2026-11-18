using Npgsql;

namespace TicketRush.Shared.Database;

public static class PostgresErrors
{
    public static bool IsUniqueViolation(this Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
