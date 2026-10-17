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

public record PageRow(PageRowId Id, RowWidths Widths, RowAlignment Alignment, RowPadding Padding, IReadOnlyList<RowSlot> Slots);

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
