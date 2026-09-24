namespace ArtistShop.Web.Domain.Publishing;

using ArtistShop.Web.Domain.Catalog;

// A post's body as the page shows it. Everything the editor might store that isn't one of these
// is dropped on the way in, so nothing a page renders came from the stored JSON unchecked
public record PostDocument(IReadOnlyList<PostBlock> Blocks);

public abstract record PostBlock;

// an empty paragraph is a blank line the artist left on purpose
public record ParagraphBlock(IReadOnlyList<PostText> Text) : PostBlock;

public record HeadingBlock(HeadingLevel Level, IReadOnlyList<PostText> Text) : PostBlock;

public record BlockquoteBlock(IReadOnlyList<PostText> Text) : PostBlock;

public record ListBlock(ListStyle Style, IReadOnlyList<IReadOnlyList<PostText>> Items) : PostBlock;

// StorageKey names the one of the artwork's images it shows. Caption is null when there is none
public record ArtworkEmbedBlock(
    ArtworkId ArtworkId,
    string StorageKey,
    EmbedImageSize Size,
    EmbedLayout Layout,
    string? Caption
) : PostBlock;

public record VideoEmbedBlock(VideoSource Source, EmbedLayout Layout) : PostBlock;

// which site plays the video, and what that site needs to find it
public abstract record VideoSource;

public record YouTubeVideo(string Id) : VideoSource;

// UnlistedHash is the second part of an unlisted video's link, without which Vimeo won't play it
public record VimeoVideo(string Id, string? UnlistedHash) : VideoSource;

// Link is null for plain text, and only ever an http, https or mailto address, or a path on this site
public record PostText(string Text, bool Bold, bool Italic, bool Underline, string? Link);

// the page title is the h1, so a post's headings start at h2
public enum HeadingLevel
{
    Two = 2,
    Three = 3,
}

public enum ListStyle
{
    Bullet,
    Ordered,
}

public enum EmbedImageSize
{
    Small,
    Medium,
}

// Left and Right sit on a line of their own; the Float ones let the text wrap around them
public enum EmbedLayout
{
    Center,
    Left,
    Right,
    FloatLeft,
    FloatRight,
}
