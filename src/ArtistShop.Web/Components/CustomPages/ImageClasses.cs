namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Domain.Website;

// an image component's choices as Tailwind classes, spelled out so Tailwind finds them
public static class ImageClasses
{
    // Fixed heights, each held to a share of a short screen, as a phone held sideways. --screen-height is
    // the row editor preview's screen; elsewhere the screen's own
    public static string Height(ImageHeight height) =>
        height switch
        {
            ImageHeight.Small => "h-[min(240px,calc(var(--screen-height,100svh)*0.4))]",
            ImageHeight.Medium => "h-[min(400px,calc(var(--screen-height,100svh)*0.6))]",
            ImageHeight.Large => "h-[min(640px,calc(var(--screen-height,100svh)*0.8))]",
            _ => throw new InvalidOperationException($"{height} has no fixed height."),
        };

    // only takes effect when the image has a height other than its own
    public static string Fit(ImageFit fit) =>
        fit switch
        {
            ImageFit.Cover => "object-cover",
            ImageFit.Contain => "object-contain",
            _ => throw new InvalidOperationException($"{fit} has no class."),
        };

    // the blurred placeholder, set as the <img>'s own background, fitted as the picture will be, so
    // under Contain it doesn't fill the space around the picture for good
    public static string PlaceholderFit(ImageFit fit) =>
        fit switch
        {
            ImageFit.Cover => "bg-cover bg-center",
            ImageFit.Contain => "bg-contain bg-center bg-no-repeat",
            _ => throw new InvalidOperationException($"{fit} has no class."),
        };
}
