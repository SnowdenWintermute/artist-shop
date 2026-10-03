namespace ArtistShop.Web.Components.Tables;

// a column's heading, with a class for a column whose cells carry one too, like one hidden on a phone
public sealed record DataTableHeading(string Text, string? Class = null)
{
    // so a heading with no class can be written as its text
    public static implicit operator DataTableHeading(string text) => new(text);
}
