namespace ArtistShop.Web.Database;

// Reproduces how the database compares names, so the names this treats as different are never ones
// a unique constraint then rejects. The case_insensitive collation on those columns ignores case
// and nothing else: accents and trailing spaces both count
public sealed class DatabaseCollationComparer : IEqualityComparer<string>
{
    public static readonly DatabaseCollationComparer Instance = new();

    private DatabaseCollationComparer() { }

    // InvariantCulture compares the way the database's collation tables do, rather than the way
    // this machine's locale would; OrdinalIgnoreCase would only fold the simple case mappings
    public bool Equals(string? left, string? right) =>
        string.Equals(left, right, StringComparison.InvariantCultureIgnoreCase);

    // a hash code has to agree with Equals, or equal names land in different buckets of a
    // HashSet and never get compared at all
    public int GetHashCode(string value) =>
        StringComparer.InvariantCultureIgnoreCase.GetHashCode(value);
}
