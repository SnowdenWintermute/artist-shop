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

// StorageKey picks one of the artwork's images; null means its primary one
public record ArtworkEmbedBlock(
    ArtworkId ArtworkId,
    string? StorageKey,
    EmbedImageSize Size,
    EmbedLayout Layout
) : PostBlock;

public record YouTubeEmbedBlock(string VideoId, EmbedLayout Layout) : PostBlock;

// Link is null for plain text, and only ever an http, https or mailto address
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

public enum EmbedLayout
{
    Center,
    FloatLeft,
    FloatRight,
}
