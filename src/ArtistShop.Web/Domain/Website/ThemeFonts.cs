namespace ArtistShop.Web.Domain.Website;

using System.Globalization;

// The two fonts a theme has. Stored by name in a theme's settings
public enum FontRole
{
    Heading = 1,
    Text = 2,
}

// Which letters a role's fonts are sized by
public enum FontSizeMeasure
{
    // body text is read by its lowercase letters
    Lowercase = 1,

    // headings by their capitals, which differ less from font to font: sized by lowercase, a font of
    // capitals only, like Bungee, would shrink to the height of another font's lowercase letters
    Capitals = 2,
}

// At 100%, a font's measured letters are as tall as ReferenceFont's, so picking another font keeps the size
public record FontRoleDefinition(
    FontRole Role,
    string Name,
    string Description,
    Font ReferenceFont,
    FontSizeMeasure Measure,
    int MaximumSizePercent
)
{
    public const int MinimumSizePercent = 70;
    public const int SizePercentStep = 5;

    // in the CSS variables, like --theme-heading-font
    public string CssName => Role.ToString().ToLowerInvariant();

    public string FontVariable => $"--theme-{CssName}-font";

    public string SizeAdjustVariable => $"--theme-{CssName}-font-size-adjust";

    public string ScaleVariable => $"--theme-{CssName}-font-scale";

    public bool AllowsSize(int sizePercent) =>
        sizePercent >= MinimumSizePercent && sizePercent <= MaximumSizePercent && sizePercent % SizePercentStep == 0;

    // the text font shows bold and italic, which a heading-only font would have the browser fake
    public bool Allows(Font font) => Role is FontRole.Heading || !Fonts.For(font).IsHeadingOnly;

    // how many times its font size the font is drawn at, which line heights grow by too
    public double Scale(Font font, int sizePercent) =>
        sizePercent / 100.0 * Measured(Fonts.For(ReferenceFont)) / Measured(Fonts.For(font));

    // font-size-adjust's value, which draws the font at Scale. A browser divides it by the x-height the
    // font states, right or wrong, so it's the stated height Scale times over
    public string SizeAdjust(Font font, int sizePercent) =>
        Css(Scale(font, sizePercent) * Fonts.For(font).RecordedXHeight);

    public string ScaleValue(Font font, int sizePercent) => Css(Scale(font, sizePercent));

    private double Measured(FontDefinition font) =>
        Measure switch
        {
            FontSizeMeasure.Lowercase => font.XHeight,
            FontSizeMeasure.Capitals => font.CapHeight,
            _ => throw new InvalidOperationException($"{Measure} isn't a font size measure."),
        };

    private static string Css(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

public static class FontRoles
{
    public static readonly IReadOnlyList<FontRoleDefinition> All =
    [
        new(FontRole.Heading, "Heading font", "Nav links and page headings.", Font.JosefinSlab, FontSizeMeasure.Capitals, 200),
        // body text much past 150% stops reading as text
        new(FontRole.Text, "Text font", "Everything else.", Font.Roboto, FontSizeMeasure.Lowercase, 150),
    ];

    public static FontRoleDefinition For(FontRole role) => All.Single(definition => definition.Role == role);
}

public sealed record FontChoice(Font Font, int SizePercent);

// Both are always chosen: unlike a colour, a font has nothing to derive from
public sealed record ThemeFonts(FontChoice Heading, FontChoice Text)
{
    public FontChoice For(FontRole role) =>
        role switch
        {
            FontRole.Heading => Heading,
            FontRole.Text => Text,
            _ => throw new InvalidOperationException($"{role} isn't a font role."),
        };

    public static ThemeFonts From(Func<FontRole, FontChoice> choose) => new(choose(FontRole.Heading), choose(FontRole.Text));
}
