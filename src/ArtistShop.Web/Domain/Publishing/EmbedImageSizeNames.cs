namespace ArtistShop.Web.Domain.Publishing;

// How each size is written in a post's Delta, as EmbedLayoutNames is for layouts
public static class EmbedImageSizeNames
{
    public static string Of(EmbedImageSize size) =>
        size switch
        {
            EmbedImageSize.Small => "small",
            EmbedImageSize.Medium => "medium",
        };

    // anything unknown, or missing, is medium
    public static EmbedImageSize Parse(string? name) => name == Of(EmbedImageSize.Small) ? EmbedImageSize.Small : EmbedImageSize.Medium;
}
