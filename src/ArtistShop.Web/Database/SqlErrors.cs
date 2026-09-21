namespace ArtistShop.Web.Database;

using Npgsql;

public static class SqlErrors
{
    // our functions RAISE their own errors with the codes in SqlStates
    public static bool IsThrown(PostgresException exception, string sqlState) =>
        exception.SqlState == sqlState;

    // Postgres names the violated constraint in its own field, so nothing has to be read out of
    // the message text
    public static bool IsUniqueConstraintViolation(PostgresException exception, string constraintName) =>
        exception.SqlState == PostgresErrorCodes.UniqueViolation
        && exception.ConstraintName == constraintName;
}
