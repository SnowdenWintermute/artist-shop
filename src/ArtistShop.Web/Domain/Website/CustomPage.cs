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

public record PageRowId(int Value);

public record RowSlotId(int Value);

// a slot's components, top to bottom
public record RowSlot(RowSlotId Id, IReadOnlyList<CustomComponent> Stack);

public record PageRow(PageRowId Id, RowWidths Widths, RowAlignment Alignment, IReadOnlyList<RowSlot> Slots);

// a page's rows, top to bottom
public record CustomPageLayout(IReadOnlyList<PageRow> Rows);
