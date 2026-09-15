namespace ArtistShop.Web.Database;

using Microsoft.Data.SqlClient;

public static class SqlErrors
{
    // SQL Server's error number for a violated PRIMARY KEY or UNIQUE constraint
    private const int UniqueConstraintViolation = 2627;

    // the message quotes the name, e.g. 'Unique_Vocabularies_Name', so matching the quotes
    // stops one constraint name matching another that starts the same way
    // our procedures THROW their own errors with numbers from 50000 up
    public static bool IsThrown(SqlException exception, int number) => exception.Number == number;

    public static bool IsUniqueConstraintViolation(SqlException exception, string constraintName) =>
        exception.Number == UniqueConstraintViolation
        && exception.Message.Contains($"'{constraintName}'");
}
