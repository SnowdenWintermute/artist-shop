namespace ArtistShop.Web.Components.Layout;

using System.Globalization;
using ArtistShop.Web.Domain.Website;

// The CSS variables a theme's frame is written as, which FrameBox reads and a custom frame sets over, and
// the lengths its choices stand for
public static class FrameCss
{
    public const string BackgroundVariable = "--theme-frame-background";
    public const string BorderVariable = "--theme-frame-border";
    public const string BorderWidthVariable = "--theme-frame-border-width";
    public const string CornersVariable = "--theme-frame-corners";
    public const string PaddingVariable = "--theme-frame-padding";

    public static string Declarations(ThemeFrame frame, ThemeColors colors) =>
        $"{BackgroundVariable}: {frame.BackgroundColor(colors).Hex}; "
        + $"{BorderVariable}: {colors.Resolve(frame.Border).Hex}; "
        + $"{BorderWidthVariable}: {Pixels(BorderWidth(frame.BorderWidth))}; "
        + $"{CornersVariable}: {Pixels(Corners(frame.Corners))}; "
        + $"{PaddingVariable}: {Pixels(Padding(frame.Padding))}; ";

    // a custom frame's own parts, set on its box over the theme's
    public static string Overrides(ComponentFrame.Custom frame) =>
        (frame.Background is { } background ? $"{BackgroundVariable}: {background.Hex}; " : "")
        + (frame.Border is { } border ? $"{BorderVariable}: {border.Hex}; " : "")
        + (frame.BorderWidth is { } width ? $"{BorderWidthVariable}: {Pixels(BorderWidth(width))}; " : "")
        + CornersOverride(frame.Corners)
        + (frame.Padding is { } padding ? $"{PaddingVariable}: {Pixels(Padding(padding))}; " : "");

    // a picture's own corners, set round it over the theme frame's; none for null, which keeps the theme's
    public static string CornersOverride(FrameCorners? corners) =>
        corners is { } chosen ? $"{CornersVariable}: {Pixels(Corners(chosen))}; " : "";

    private static int BorderWidth(FrameBorderWidth width) =>
        width switch
        {
            FrameBorderWidth.None => 0,
            FrameBorderWidth.Thin => 1,
            FrameBorderWidth.Medium => 2,
            FrameBorderWidth.Thick => 4,
            _ => throw new InvalidOperationException($"{width} has no width."),
        };

    private static int Corners(FrameCorners corners) =>
        corners switch
        {
            FrameCorners.None => 0,
            FrameCorners.Small => 4,
            FrameCorners.Medium => 8,
            FrameCorners.Large => 16,
            _ => throw new InvalidOperationException($"{corners} has no radius."),
        };

    private static int Padding(FramePadding padding) =>
        padding switch
        {
            FramePadding.None => 0,
            FramePadding.Small => 8,
            FramePadding.Medium => 16,
            FramePadding.Large => 32,
            _ => throw new InvalidOperationException($"{padding} has no padding."),
        };

    private static string Pixels(int pixels) => $"{pixels.ToString(CultureInfo.InvariantCulture)}px";
}
