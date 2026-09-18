namespace ArtistShop.Web.Database;

// Reproduces how the database compares strings, so a pair of names this accepts is never a pair
// dbo.ArtworkNameList's primary key then rejects. The collation DatabaseInitializer pins ignores
// case, and SQL Server ignores trailing spaces in an equality comparison (leading ones count)
public sealed class DatabaseCollationComparer : IEqualityComparer<string>
{
    public static readonly DatabaseCollationComparer Instance = new();

    private DatabaseCollationComparer() { }

    // InvariantCulture compares the way the database's collation tables do, rather than the way
    // this machine's locale would; OrdinalIgnoreCase would only fold the simple case mappings
    public bool Equals(string? left, string? right) =>
        string.Equals(left?.TrimEnd(), right?.TrimEnd(), StringComparison.InvariantCultureIgnoreCase);

    // a hash code has to agree with Equals, or equal names land in different buckets of a
    // HashSet and never get compared at all
    public int GetHashCode(string value) =>
        StringComparer.InvariantCultureIgnoreCase.GetHashCode(value.TrimEnd());
}
