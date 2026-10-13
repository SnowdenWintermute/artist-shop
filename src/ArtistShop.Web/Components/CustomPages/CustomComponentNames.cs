namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Domain.Website;

// what the admin calls each type, in the website's words
public static class CustomComponentNames
{
    public static string Of(CustomComponentType type, SiteWording wording) =>
        type switch
        {
            CustomComponentType.CollectionViewer => $"{wording.Collection.SingularHeading} viewer",
            CustomComponentType.PostList => "Post list",
            _ => throw new InvalidOperationException($"{type} has no name."),
        };
}
