namespace ArtistShop.Web.Utilities;

// the pages something is on, as admin pages list them: by name, between commas
public static class PageNamesText
{
    public static string Of(IEnumerable<string> pageNames) =>
        string.Join(", ", pageNames.Order(NameOrder.ByName));
}
