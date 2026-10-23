namespace ArtistShop.Web.Domain.Publishing;

using ArtistShop.Web.Domain.Catalog;

// Text from the editor as the page shows it. Everything the editor might store that isn't one of these
// is dropped on the way in, so nothing a page renders came from the stored JSON unchecked
public record RichTextDocument(IReadOnlyList<RichTextBlock> Blocks);

public abstract record RichTextBlock;

// an empty paragraph is a blank line the artist left on purpose
public record ParagraphBlock(IReadOnlyList<RichTextRun> Text) : RichTextBlock;

// a line in the theme's heading font; its words carry their own sizes, as any line's do
public record HeadingBlock(IReadOnlyList<RichTextRun> Text) : RichTextBlock;

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
public record RichTextRun(string Text, bool Bold, bool Italic, bool Underline, string? Link, TextSize Size);

// The sizes text can be, numbered by its pixels in a font at 100%. 16 is the page's own text size
public enum TextSize
{
    Size14 = 14,
    Size16 = 16,
    Size18 = 18,
    Size24 = 24,
    Size36 = 36,
    Size48 = 48,
    Size60 = 60,
    Size72 = 72,
    Size96 = 96,
    Size128 = 128,
    Size160 = 160,
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
