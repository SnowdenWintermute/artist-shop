namespace ArtistShop.Web.Components.Publishing;

using ArtistShop.Web.Domain.Publishing;

// a line of rich text's alignment, as Tailwind classes spelled out so Tailwind finds them. Left has
// none, so the text keeps the page's own alignment, as before lines could be aligned
public static class TextAlignmentClasses
{
    public static string Text(TextAlignment alignment) =>
        alignment switch
        {
            TextAlignment.Left => "",
            TextAlignment.Center => "text-center",
            TextAlignment.Right => "text-right",
            _ => throw new InvalidOperationException($"{alignment} has no class."),
        };

    // An aligned list's markers move with its text, as they do in the editor, rather than staying
    // in the margin
    public static string List(TextAlignment alignment) =>
        alignment is TextAlignment.Left ? "pl-6" : $"list-inside {Text(alignment)}";
}
