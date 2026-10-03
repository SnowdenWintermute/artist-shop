namespace ArtistShop.Web.Domain.Sites;

// The inputs that come back the way their member left them. The names are stored, so renaming one
// forgets every value saved under the old name
public static class RememberedInputs
{
    public const string DashboardArtworkType = "dashboard-artwork-type";

    // what the saving endpoint accepts, so it can't fill the table with names nothing reads
    public static readonly IReadOnlySet<string> Names = new HashSet<string> { DashboardArtworkType };
}
