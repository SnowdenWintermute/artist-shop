namespace ArtistShop.Web.Database;

using Microsoft.Data.SqlClient;

public class SqlConnectionFactory(string connectionString)
{
    public SqlConnection Create() => new(connectionString);
}
