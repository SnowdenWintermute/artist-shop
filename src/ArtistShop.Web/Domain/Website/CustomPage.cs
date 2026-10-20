namespace ArtistShop.Web.Domain.Website;

public record CustomPageId(int Value);

// the page's address is /Value: lower case letters, numbers and dashes, as a slug
public record CustomPageAddress(string Value)
{
    public static CustomPageAddress FromName(string name) => new(ArtistShopSlug.FromName(name));

    public bool IsWellFormed => ArtistShopSlug.IsWellFormed(Value);
}

// A page the artist built. The home page is shown at / rather than at its own address; the blog page
// is where a post's up link goes
public record CustomPage(CustomPageId Id, string Name, CustomPageAddress Address, bool IsHome, bool IsBlog);

// the ids are stored in page_rows.widths. Each is a fixed split of the row between its slots, left
// to right, so the number of slots follows from it
public enum RowWidths
{
    Whole = 1,
    Halves = 2,
    ThirdThenTwoThirds = 3,
    TwoThirdsThenThird = 4,
    Thirds = 5,
    QuarterHalfQuarter = 6,
}

// the splits offered for each number of slots, the first being the one a row gets when it reaches that number
public static class RowWidthChoices
{
    public const int MaximumSlotCount = 3;

    public static IReadOnlyList<RowWidths> For(int slotCount) =>
        slotCount switch
        {
            1 => [RowWidths.Whole],
            2 => [RowWidths.Halves, RowWidths.ThirdThenTwoThirds, RowWidths.TwoThirdsThenThird],
            3 => [RowWidths.Thirds, RowWidths.QuarterHalfQuarter],
            _ => throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "A row has one to three slots."),
        };
}

// the ids are stored in page_rows.alignment: where slots of different heights line up
public enum RowAlignment
{
    Start = 1,
    Center = 2,
    End = 3,
}

// the ids are stored in page_rows.height. FillScreen makes the row at least a screen high: the
// first row, what the nav leaves; the last, with the footer in it
public enum RowHeight
{
    Natural = 1,
    FillScreen = 2,
}

// the ids are stored in page_rows.content_width: how wide the row's content may run. Edge to edge
// runs it across the whole screen
public enum RowContentWidth
{
    Page = 1,
    EdgeToEdge = 2,
}

// the ids are stored in page_rows.gap: the space between a row's slots, and between the components
// stacked in each
public enum RowGap
{
    None = 1,
    Small = 2,
    Medium = 3,
    Large = 4,
}

// the ids are stored in page_rows.background_type
public enum RowBackgroundType
{
    None = 1,
    Color = 2,
    Image = 3,
    Slideshow = 4,
}

// the ids are stored in page_rows.background_slide_interval: how long each of a slideshow's images shows, slower
// than a carousel's, as it's behind the content
public enum SlideshowInterval
{
    SixSeconds = 1,
    TenSeconds = 2,
    FifteenSeconds = 3,
}

// what's drawn behind a row's content, across the whole screen whatever its content's width
public abstract record RowBackground
{
    public abstract RowBackgroundType Type { get; }

    // a colour's, or the one behind an image; null where the row's page colour shows
    public RgbColor? DrawnColor =>
        this switch
        {
            Color color => color.Value,
            Image image => image.Behind,
            Slideshow slideshow => slideshow.Behind,
            _ => null,
        };

    // an image's or slideshow's; null for a background with no pictures
    public ImageFit? PictureFit =>
        this switch
        {
            Image image => image.Fit,
            Slideshow slideshow => slideshow.Fit,
            _ => null,
        };

    public sealed record None : RowBackground
    {
        public override RowBackgroundType Type => RowBackgroundType.None;
    }

    // stored in page_rows.background_color
    public sealed record Color(RgbColor Value) : RowBackground
    {
        public override RowBackgroundType Type => RowBackgroundType.Color;
    }

    // An image across the row, centred. Source is null until one is chosen. Behind is the colour around
    // an image that doesn't cover the row, null for the row's page colour, and what's left when the
    // image is deleted
    public sealed record Image(ImageSource? Source, ImageFit Fit, RgbColor? Behind) : RowBackground
    {
        public override RowBackgroundType Type => RowBackgroundType.Image;
    }

    // Images crossfading across the row, each centred and fitted, the visitor able to pause them. Behind is as an
    // Image's
    public sealed record Slideshow(SlideSource Source, SlideshowInterval Interval, ImageFit Fit, RgbColor? Behind) : RowBackground
    {
        public override RowBackgroundType Type => RowBackgroundType.Slideshow;

        public static TimeSpan IntervalOf(SlideshowInterval interval) =>
            interval switch
            {
                SlideshowInterval.SixSeconds => TimeSpan.FromSeconds(6),
                SlideshowInterval.TenSeconds => TimeSpan.FromSeconds(10),
                SlideshowInterval.FifteenSeconds => TimeSpan.FromSeconds(15),
                _ => throw new InvalidOperationException($"{interval} has no length."),
            };
    }
}

// the ids are stored in page_rows.top_padding and bottom_padding. Auto leaves more above a page's
// first row and below its last than between rows
public enum RowVerticalPadding
{
    Auto = 1,
    None = 2,
    Small = 3,
    Medium = 4,
    Large = 5,
}

// the ids are stored in page_rows.side_padding
public enum RowSidePadding
{
    None = 1,
    Small = 2,
    Medium = 3,
    Large = 4,
}

// the space inside a row around its content
public record RowPadding(RowVerticalPadding Top, RowVerticalPadding Bottom, RowSidePadding Sides)
{
    // what a new row gets, which matches pages from before rows had padding
    public static readonly RowPadding New = new(RowVerticalPadding.Auto, RowVerticalPadding.Auto, RowSidePadding.Medium);
}

public record PageRowId(int Value);

public record RowSlotId(int Value);

// a slot's components, top to bottom
public record RowSlot(RowSlotId Id, IReadOnlyList<CustomComponent> Stack);

// What the row editor's settings form saves in one go. Theme is null for the website's
public record RowSettings(
    RowHeight Height,
    RowContentWidth ContentWidth,
    RowPadding Padding,
    RowGap Gap,
    RowAlignment Alignment,
    ThemeKey? Theme,
    RowBackground Background
);

// Theme is the row's own, a preset or a saved theme, drawn with its page colour behind it; null follows
// the website's theme, as does a preset since retired
public record PageRow(
    PageRowId Id,
    RowWidths Widths,
    RowAlignment Alignment,
    RowHeight Height,
    RowContentWidth ContentWidth,
    RowPadding Padding,
    RowGap Gap,
    NamedTheme? Theme,
    RowBackground Background,
    IReadOnlyList<RowSlot> Slots
)
{
    public RowSettings Settings => new(Height, ContentWidth, Padding, Gap, Alignment, Theme?.Key, Background);
}

// a page's rows, top to bottom
public record CustomPageLayout(IReadOnlyList<PageRow> Rows)
{
    // null when the row isn't on this page, as when it was removed
    public PageRow? Row(PageRowId id) => Rows.FirstOrDefault(row => row.Id == id);

    public RowPosition PositionOf(PageRow row)
    {
        var index = Rows.ToList().FindIndex(each => each.Id == row.Id);

        return index >= 0
            ? new RowPosition(index == 0, index == Rows.Count - 1)
            : throw new InvalidOperationException($"Row {row.Id.Value} isn't on this page.");
    }
}

// where a row sits on its page, which its padding depends on
public record RowPosition(bool IsFirst, bool IsLast);
