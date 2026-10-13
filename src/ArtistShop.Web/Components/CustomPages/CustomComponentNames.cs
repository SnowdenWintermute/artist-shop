namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Domain.Website;

// what the admin calls each type, in the website's words, and the names new components get from it
public static class CustomComponentNames
{
    // "Article 1", then "Article 2" and on, skipping names already taken
    public static IEnumerable<string> NewNames(CustomComponentType type, SiteWording wording, IReadOnlySet<string> taken) =>
        Enumerable
            .Range(1, int.MaxValue)
            .Select(number => $"{Of(type, wording)} {number}")
            .Where(name => !taken.Contains(name));

    public static string Of(CustomComponentType type, SiteWording wording) =>
        type switch
        {
            CustomComponentType.CollectionViewer => $"{wording.Collection.SingularHeading} viewer",
            CustomComponentType.PostList => "Post list",
            CustomComponentType.Article => "Article",
            _ => throw new InvalidOperationException($"{type} has no name."),
        };
}
