namespace ArtistShop.Web.Domain.Sites;

// A deleted site is offline for this long, and its owner can keep it, before it's erased
public static class SiteDeletion
{
    public const int GraceDays = 30;

    public static DateTimeOffset EraseAt(DateTimeOffset deletedAt) => deletedAt.AddDays(GraceDays);
}
