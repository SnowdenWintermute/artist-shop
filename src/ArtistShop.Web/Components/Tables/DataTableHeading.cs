namespace ArtistShop.Web.Components.Tables;

// a column's heading, with a class for a column whose cells carry one too, like one hidden on a phone.
// Pinned: a column the caller keeps in view with sticky left-*, which the other headings scroll under
public sealed record DataTableHeading(string Text, string? Class = null, bool Pinned = false)
{
    // so a heading with no class can be written as its text
    public static implicit operator DataTableHeading(string text) => new(text);
}
