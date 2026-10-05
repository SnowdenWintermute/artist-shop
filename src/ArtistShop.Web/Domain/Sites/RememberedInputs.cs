namespace ArtistShop.Web.Domain.Sites;

// The inputs that come back the way their member left them. The names are stored, so renaming one
// forgets every value saved under the old name
public static class RememberedInputs
{
    // shared by every artwork type select, through RememberedArtworkType
    public const string ArtworkType = "artwork-type";

    // what the saving endpoint accepts, so it can't fill the table with names nothing reads
    public static readonly IReadOnlySet<string> Names = new HashSet<string> { ArtworkType };
}
