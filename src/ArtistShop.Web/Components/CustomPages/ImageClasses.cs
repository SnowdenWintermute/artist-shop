namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Domain.Website;

// an image component's choices as Tailwind classes, spelled out so Tailwind finds them
public static class ImageClasses
{
    public static string Padding(ImagePadding padding) =>
        padding switch
        {
            ImagePadding.None => "",
            ImagePadding.Small => "p-4",
            ImagePadding.Medium => "p-8",
            ImagePadding.Large => "p-16",
            _ => throw new InvalidOperationException($"{padding} has no class."),
        };

    // only takes effect once the row gives the image a height of its own
    public static string Fit(ImageFit fit) =>
        fit switch
        {
            ImageFit.Cover => "object-cover",
            ImageFit.Contain => "object-contain",
            _ => throw new InvalidOperationException($"{fit} has no class."),
        };
}
