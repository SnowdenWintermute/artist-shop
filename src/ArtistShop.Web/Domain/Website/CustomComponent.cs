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
    Heading = 6,
    Button = 7,
}

// One record per type, holding what the artist chose. Kept in custom_components.settings as JSON
// (CustomComponentSettingsJson), so a new setting needs no migration
public abstract record CustomComponentSettings
{
    [JsonIgnore]
    public abstract CustomComponentType Type { get; }

    // whether it can have a frame (ComponentFrame), which one without always has Off
    [JsonIgnore]
    public virtual bool HasFrame => true;
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

// which side of its slot a heading's text or a button sits on. The ids are only for ordering; settings store the names
public enum ComponentAlignment
{
    Left = 1,
    Center = 2,
    Right = 3,
}

// one line of heading text, at any of the editor's sizes, in either of the theme's fonts, linking somewhere or not
public sealed record HeadingSettings(
    string Text,
    TextSize Size,
    FontRole Font,
    bool Bold,
    ComponentAlignment Alignment,
    LinkTarget? LinkTarget
) : CustomComponentSettings
{
    public override CustomComponentType Type => CustomComponentType.Heading;

    public static readonly HeadingSettings New = new("Heading", TextSize.Size36, FontRole.Heading, Bold: true, ComponentAlignment.Left, LinkTarget: null);
}

// A button's look: its colours fill it, or only draw its border and text. The ids are only for ordering;
// settings store the names
public enum ButtonStyle
{
    Filled = 1,
    Outline = 2,
}

// Which of the theme's colours a button takes: Accent the accent with its own text colour, Primary the text
// colour on the page colour, Normal a panel with a line round it. The ids are only for ordering; settings
// store the names
public enum ButtonColor
{
    Accent = 1,
    Primary = 2,
    Normal = 3,
}

// A link drawn as a button, going to its LinkTarget: a new one goes to the website's home page, so it shows at
// once. Deleting what it links to deletes the button (Mike, 2026-10-23). It has no frame, being a box itself.
// Size is one of the heading's sizes, which the button's padding grows with. BorderWidth and Corners null take the
// theme frame's, as a custom frame's parts do
public sealed record ButtonSettings(
    string Text,
    LinkTarget LinkTarget,
    ComponentAlignment Alignment,
    ButtonStyle Style,
    TextSize Size,
    ButtonColor Color,
    FrameBorderWidth? BorderWidth,
    FrameCorners? Corners
) : CustomComponentSettings
{
    public override CustomComponentType Type => CustomComponentType.Button;

    public override bool HasFrame => false;

    public static ButtonSettings New(LinkTarget target) =>
        new("Learn more", target, ComponentAlignment.Left, ButtonStyle.Filled, TextSize.Size24, ButtonColor.Accent, BorderWidth: null, Corners: null);
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

// what clicking an image does. ArtworkPage is only for a work's image; Target goes to the image's LinkTarget
public enum ImageLink
{
    Lightbox = 1,
    ArtworkPage = 2,
    None = 3,
    Target = 4,
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
// chosen, and again once its image is deleted. LinkTarget is set exactly when Link is Target. Corners round the
// picture, null as the theme rounds frames
public sealed record ImageSettings(
    ImageSource? Source,
    ImageHeight Height,
    ImageFit Fit,
    ImageLink Link,
    LinkTarget? LinkTarget,
    FrameCorners? Corners
) : CustomComponentSettings
{
    public override CustomComponentType Type => CustomComponentType.Image;

    public static readonly ImageSettings New = new(null, ImageHeight.Natural, ImageFit.Cover, ImageLink.Lightbox, LinkTarget: null, Corners: null);

    // whether Fit changes anything, wherever the image is placed
    public static bool HasFit(ImageHeight height) => height is not ImageHeight.Natural;
}

// Name is for finding it again in the library and is unique; a page never shows it
public record CustomComponent(CustomComponentId Id, string Name, CustomComponentSettings Settings, ComponentFrame Frame);

// a component and the pages it's on, for saying what a delete takes with it
public record CustomComponentOnPages(CustomComponentId Id, string Name, IReadOnlyList<string> PageNames);

// the components linking to what's about to be deleted: the buttons, deleted with it, and the rest, which lose
// the link
public record LinkingComponents(IReadOnlyList<CustomComponentOnPages> DeletedButtons, IReadOnlyList<CustomComponentOnPages> Unlinked)
{
    public static readonly LinkingComponents None = new([], []);

    public bool IsEmpty => DeletedButtons.Count == 0 && Unlinked.Count == 0;
}

// A component with the names of the pages it's on, for choosing one already made or editing it.
// UpdatedAt tells the settings page's backup whether it was saved since the copy was made. The blog page always
// holds a post list and the collections page a card grid of all collections, so the last of either stays
public record CustomComponentInUse(
    CustomComponent Component,
    IReadOnlyList<string> PageNames,
    DateTimeOffset UpdatedAt,
    bool IsBlogPagesOnlyPostList,
    bool IsCollectionsPagesOnlyGrid
)
{
    public bool CanBeDeleted => !IsBlogPagesOnlyPostList && !IsCollectionsPagesOnlyGrid;
}

// What the Add component dialog's New buttons make: one of each type. A card grid starts as the
// collection viewer
public enum CustomComponentStarter
{
    CollectionViewer = 1,
    PostList = 2,
    Text = 3,
    Heading = 4,
    Image = 5,
    Carousel = 6,
    Button = 7,
}

// the settings each starter makes a component with. HomeId is the website's home page, which a new button links to
public static class NewCustomComponentSettings
{
    public static CustomComponentSettings For(CustomComponentStarter starter, CustomPageId homeId) =>
        starter switch
        {
            CustomComponentStarter.CollectionViewer => CardGridSettings.AllCollections,
            CustomComponentStarter.PostList => new PostListSettings(),
            CustomComponentStarter.Text => new TextSettings(RichText.Empty, TextWidth.Narrow),
            CustomComponentStarter.Heading => HeadingSettings.New,
            CustomComponentStarter.Image => ImageSettings.New,
            CustomComponentStarter.Carousel => CarouselSettings.New,
            CustomComponentStarter.Button => ButtonSettings.New(new LinkTarget.Page(homeId)),
            _ => throw new InvalidOperationException($"{starter} has no starting settings."),
        };
}
