namespace ArtistShop.Web.Domain.Website;

using System.Globalization;

// A colour as CSS writes it in hex: "#rrggbb", or "#rrggbbaa" when it's partly see-through
public readonly record struct RgbColor(byte Red, byte Green, byte Blue, byte Alpha = 255)
{
    public static readonly RgbColor Black = new(0, 0, 0);
    public static readonly RgbColor White = new(255, 255, 255);

    public bool IsOpaque => Alpha == 255;

    public string Hex =>
        IsOpaque ? $"#{Red:x2}{Green:x2}{Blue:x2}" : $"#{Red:x2}{Green:x2}{Blue:x2}{Alpha:x2}";

    // how much of the colour shows, as the slider on the Theme page says it
    public int OpacityPercent => (int)Math.Round(Alpha / 2.55);

    public static RgbColor Parse(string hex) =>
        TryParse(hex, out var color) ? color : throw new FormatException($"\"{hex}\" isn't a hex colour.");

    public static bool TryParse(string? hex, out RgbColor color)
    {
        color = default;

        if (hex is not { Length: 7 or 9 } || hex[0] != '#')
        {
            return false;
        }

        Span<byte> channels = [0, 0, 0, 255];

        for (var index = 0; index < (hex.Length - 1) / 2; index++)
        {
            if (!byte.TryParse(hex.AsSpan(1 + index * 2, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out channels[index]))
            {
                return false;
            }
        }

        color = new RgbColor(channels[0], channels[1], channels[2], channels[3]);
        return true;
    }

    public RgbColor WithOpacityPercent(int percent) => this with { Alpha = (byte)Math.Round(Math.Clamp(percent, 0, 100) * 2.55) };

    // this colour moved part of the way to the other, by amount from 0 (this) to 1 (the other)
    public RgbColor Mix(RgbColor other, double amount) =>
        new(Channel(Red, other.Red, amount), Channel(Green, other.Green, amount), Channel(Blue, other.Blue, amount));

    // WCAG's contrast ratio, from 1 (the same) to 21 (black on white). Text needs 4.5 to be easy to read
    public static double Contrast(RgbColor first, RgbColor second)
    {
        var (lighter, darker) = (Math.Max(first.Luminance, second.Luminance), Math.Min(first.Luminance, second.Luminance));
        return (lighter + 0.05) / (darker + 0.05);
    }

    // black or white, whichever is easier to read on this colour
    public RgbColor ReadableText => Contrast(this, Black) >= Contrast(this, White) ? Black : White;

    public bool IsLight => ReadableText == Black;

    // WCAG's relative luminance
    private double Luminance => 0.2126 * Linear(Red) + 0.7152 * Linear(Green) + 0.0722 * Linear(Blue);

    private static double Linear(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static byte Channel(byte from, byte to, double amount) => (byte)Math.Round(from + (to - from) * amount);
}
