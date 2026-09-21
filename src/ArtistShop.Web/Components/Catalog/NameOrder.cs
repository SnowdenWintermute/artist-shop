namespace ArtistShop.Web.Components.Catalog;

// How the artist reads a list of names, whatever they name: every ...Order class sorts by this one
public static class NameOrder
{
    public static readonly StringComparer ByName = StringComparer.CurrentCultureIgnoreCase;
}
