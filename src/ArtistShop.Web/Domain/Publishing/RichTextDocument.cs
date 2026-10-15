namespace ArtistShop.Web.Domain.Publishing;

using ArtistShop.Web.Domain.Catalog;

// Text from the editor as the page shows it. Everything the editor might store that isn't one of these
// is dropped on the way in, so nothing a page renders came from the stored JSON unchecked
public record RichTextDocument(IReadOnlyList<RichTextBlock> Blocks);

public abstract record RichTextBlock;

// an empty paragraph is a blank line the artist left on purpose
public record ParagraphBlock(IReadOnlyList<RichTextRun> Text) : RichTextBlock;

public record HeadingBlock(HeadingLevel Level, IReadOnlyList<RichTextRun> Text) : RichTextBlock;

public record BlockquoteBlock(IReadOnlyList<RichTextRun> Text) : RichTextBlock;

public record ListBlock(ListStyle Style, IReadOnlyList<IReadOnlyList<RichTextRun>> Items) : RichTextBlock;

// StorageKey names the one of the work's images it shows. Caption is null when there is none
public record WorkEmbedBlock(
    WorkId WorkId,
    string StorageKey,
    EmbedImageSize Size,
    EmbedLayout Layout,
    string? Caption
) : RichTextBlock;

// A library image. Its size, blur and alt text are the library's, loaded beside the document.
// OpensLightbox is what the artist checked; the page also needs a version wider than the size
// before a click opens one
public record UploadedImageEmbedBlock(
    string StorageKey,
    EmbedImageSize Size,
    EmbedLayout Layout,
    string? Caption,
    bool OpensLightbox
) : RichTextBlock;

public record VideoEmbedBlock(VideoSource Source, EmbedLayout Layout) : RichTextBlock;

// which site plays the video, and what that site needs to find it
public abstract record VideoSource;

public record YouTubeVideo(string Id) : VideoSource;

// UnlistedHash is the second part of an unlisted video's link, without which Vimeo won't play it
public record VimeoVideo(string Id, string? UnlistedHash) : VideoSource;

// Link is null for plain text, and only ever an http, https or mailto address, or a path on this site
public record RichTextRun(string Text, bool Bold, bool Italic, bool Underline, string? Link);

// The editor's heading levels, largest first, as Quill numbers them in the Delta, and drawn as h1 to h3.
// One came later, for very large text, above the two posts had
public enum HeadingLevel
{
    One = 1,
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
    FloatLeft,
    FloatRight,
}
