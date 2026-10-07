namespace ArtistShop.Web.Components.Tables;

using Microsoft.AspNetCore.Components;

// a column's heading, with a class for a column whose cells carry one too, like one hidden on a phone.
// Pinned: a column the caller keeps in view with sticky left-*, which the other headings scroll under.
// Content in place of the text, such as a select-all checkbox
public sealed record DataTableHeading(string Text, string? Class = null, bool Pinned = false, RenderFragment? Content = null)
{
    // so a heading with no class can be written as its text
    public static implicit operator DataTableHeading(string text) => new(text);
}
