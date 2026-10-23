namespace ArtistShop.Web.Domain.Publishing;

using System.Globalization;

// a size as the editor stores it and the page's class names it: its pixels
public static class TextSizeNames
{
    public static string Of(TextSize size) => ((int)size).ToString(CultureInfo.InvariantCulture);

    // anything unknown, or missing, is the page's own size
    public static TextSize Parse(string? name) => Enum.GetValues<TextSize>().FirstOrDefault(size => Of(size) == name, TextSize.Size16);

    // defined in app.css
    public static string ClassOf(TextSize size) => $"text-size-{Of(size)}";
}
