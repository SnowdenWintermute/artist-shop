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

// the ids are stored in page_rows.alignment: where slots of different heights line up
public enum RowAlignment
{
    Start = 1,
    Center = 2,
    End = 3,
}

// a slot's components, top to bottom
public record RowSlot(IReadOnlyList<CustomComponent> Stack);

public record PageRow(RowWidths Widths, RowAlignment Alignment, IReadOnlyList<RowSlot> Slots);

// a page's rows, top to bottom
public record CustomPageLayout(IReadOnlyList<PageRow> Rows);
