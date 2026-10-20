namespace ArtistShop.Web.Domain.Website;

using System.Text.Json.Serialization;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;

public record CustomComponentId(int Value);

// the ids are stored in custom_components.type: the templates an artist makes components from
public enum CustomComponentType
{
    CardGrid = 1,
    PostList = 2,
    Text = 3,
    Image = 4,
    Carousel = 5,
}

// One record per type, holding what the artist chose. Kept in custom_components.settings as JSON
// (CustomComponentSettingsJson), so a new setting needs no migration
public abstract record CustomComponentSettings
{
    [JsonIgnore]
    public abstract CustomComponentType Type { get; }
}

// the published posts, newest first, a page of them at a time
public sealed record PostListSettings : CustomComponentSettings
{
    public override CustomComponentType Type => CustomComponentType.PostList;
}

// how wide a text component's text runs in its slot. The ids are only for ordering; settings store the names
public enum TextWidth
{
    // a column of the post's width, centred, so lines stay a readable length in a wide slot
    Narrow = 1,
    Full = 2,
}

// text written in the post editor, with its artwork, image and video embeds
public sealed record TextSettings(RichText Body, TextWidth Width) : CustomComponentSettings
{
    public override CustomComponentType Type => CustomComponentType.Text;
}

// How tall an image is drawn. Natural is its own shape; the sizes are fixed heights, each held to a
// share of a short screen; Fill takes what's left of its slot, which is as tall as its row, and is
// never shorter than Natural except in a row that fills the screen. The ids are only for ordering;
// settings store the names
public enum ImageHeight
{
    Natural = 1,
    Small = 2,
    Medium = 3,
    Large = 4,
    Fill = 5,
}

// How an image fills the box its height gives it, which Natural's never needs. The ids are only for
// ordering; settings store the names
public enum ImageFit
{
    // fills the box, cropping what overflows
    Cover = 1,
    // the whole image, leaving the rest of the box empty
    Contain = 2,
}

// the space around an image inside its slot
public enum ImagePadding
{
    None = 1,
    Small = 2,
    Medium = 3,
    Large = 4,
}

// what clicking an image does. ArtworkPage is only for a work's image
public enum ImageLink
{
    Lightbox = 1,
    ArtworkPage = 2,
    None = 3,
}

public enum ImageSourceType
{
    Library = 1,
    Work = 2,
}

// where an image component's picture comes from
public abstract record ImageSource
{
    public abstract ImageSourceType Type { get; }

    // as forms and columns hold one: with a work's id, the key of the work's image picked (null for its
    // main one); without, a library image's key. Null for neither
    public static ImageSource? Of(int? workId, string? storageKey) =>
        workId is int id ? new Work(new WorkId(id), storageKey)
        : storageKey is { } key ? new Library(key)
        : null;

    // an image in the site's image library, by its file's key, as text embeds keep theirs
    public sealed record Library(string StorageKey) : ImageSource
    {
        public override ImageSourceType Type => ImageSourceType.Library;
    }

    // One of a work's images, by its file's key, or its main image when null, so a change of the work's
    // main image shows here too. The alt text is the work's title
    public sealed record Work(WorkId WorkId, string? StorageKey) : ImageSource
    {
        public override ImageSourceType Type => ImageSourceType.Work;

        // the shown image's place among the work's, null when it has none or no longer has the one picked
        public int? ImageIndexOf(Catalog.Work work)
        {
            var index = StorageKey is null
                ? work.MainImageIndex
                : work.Images.Select(image => image.StorageKey).ToList().IndexOf(StorageKey);

            return index >= 0 && index < work.Images.Count ? index : null;
        }
    }
}

// One image, meant as a hero image rather than a picture in a text. Source is null until one is
// chosen, and again once its image is deleted
public sealed record ImageSettings(ImageSource? Source, ImageHeight Height, ImageFit Fit, ImagePadding Padding, ImageLink Link)
    : CustomComponentSettings
{
    public override CustomComponentType Type => CustomComponentType.Image;

    public static readonly ImageSettings New = new(null, ImageHeight.Natural, ImageFit.Cover, ImagePadding.None, ImageLink.Lightbox);

    // whether Fit changes anything, wherever the image is placed
    public static bool HasFit(ImageHeight height) => height is not ImageHeight.Natural;
}

// Name is for finding it again in the library and is unique; a page never shows it
public record CustomComponent(CustomComponentId Id, string Name, CustomComponentSettings Settings);

// a component and the pages it's on, for saying what a delete takes with it
public record CustomComponentOnPages(CustomComponentId Id, string Name, IReadOnlyList<string> PageNames);

// A component with the names of the pages it's on, for choosing one already made or editing it.
// UpdatedAt tells the settings page's backup whether it was saved since the copy was made
public record CustomComponentInUse(CustomComponent Component, IReadOnlyList<string> PageNames, DateTimeOffset UpdatedAt);

// What the Add component dialog's New buttons make: one of each type, and Heading, a text component
// that starts as one large heading line, for an artist who wants only that. A card grid starts as
// the collection viewer
public enum CustomComponentStarter
{
    CollectionViewer = 1,
    PostList = 2,
    Text = 3,
    Heading = 4,
    Image = 5,
    Carousel = 6,
}

// the settings each starter makes a component with
public static class NewCustomComponentSettings
{
    public static CustomComponentSettings For(CustomComponentStarter starter) =>
        starter switch
        {
            CustomComponentStarter.CollectionViewer => CardGridSettings.AllCollections,
            CustomComponentStarter.PostList => new PostListSettings(),
            CustomComponentStarter.Text => new TextSettings(RichText.Empty, TextWidth.Narrow),
            CustomComponentStarter.Heading => new TextSettings(HeadingText, TextWidth.Full),
            CustomComponentStarter.Image => ImageSettings.New,
            CustomComponentStarter.Carousel => CarouselSettings.New,
            _ => throw new InvalidOperationException($"{starter} has no starting settings."),
        };

    private static readonly RichText HeadingText = RichTextDeltaWriter.Write(
        new RichTextDocument([new HeadingBlock(HeadingLevel.One, [new RichTextRun("Heading", false, false, false, null)])])
    );
}
