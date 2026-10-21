namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Domain.Website;

// what the admin calls each type, in the website's words, and the names new components get from it
public static class CustomComponentNames
{
    // "Text 1", then "Text 2" and on, skipping names already taken
    public static IEnumerable<string> NewNames(
        CustomComponentStarter starter,
        SiteWording wording,
        IReadOnlySet<string> taken
    ) =>
        Enumerable
            .Range(1, int.MaxValue)
            .Select(number => $"{Of(starter, wording)} {number}")
            .Where(name => !taken.Contains(name));

    // a New button's label, and the start of the names it gives
    public static string Of(CustomComponentStarter starter, SiteWording wording) =>
        starter switch
        {
            CustomComponentStarter.CollectionViewer =>
                $"{wording.Collection.SingularHeading} viewer",
            CustomComponentStarter.PostList => Of(CustomComponentType.PostList, wording),
            CustomComponentStarter.Text => Of(CustomComponentType.Text, wording),
            CustomComponentStarter.Heading => Of(CustomComponentType.Heading, wording),
            CustomComponentStarter.Image => Of(CustomComponentType.Image, wording),
            CustomComponentStarter.Carousel => Of(CustomComponentType.Carousel, wording),
            _ => throw new InvalidOperationException($"{starter} has no name."),
        };

    public static string Of(CustomComponentType type, SiteWording wording) =>
        type switch
        {
            CustomComponentType.CardGrid => "Card grid",
            CustomComponentType.PostList => "Post list",
            CustomComponentType.Text => "Text / article",
            CustomComponentType.Image => "Image",
            CustomComponentType.Carousel => "Carousel",
            CustomComponentType.Heading => "Heading",
            _ => throw new InvalidOperationException($"{type} has no name."),
        };
}
