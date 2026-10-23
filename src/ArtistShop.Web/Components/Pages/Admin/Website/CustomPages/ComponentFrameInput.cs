using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

// A component's frame as its settings form posts it. A custom frame's parts left null, and its colours not
// marked as its own, take the theme's. The pickers hold the theme's colours until one is picked, so choosing
// an own colour starts from what shows
public class ComponentFrameInput
{
    public ComponentFrameType Type { get; set; }

    public bool HasOwnBackground { get; set; }

    // what the colour picker holds, "#rrggbb"
    public string? Background { get; set; }

    public int BackgroundOpacityPercent { get; set; }

    public bool HasOwnBorder { get; set; }

    public string? Border { get; set; }

    public FrameBorderWidth? BorderWidth { get; set; }

    public FrameCorners? Corners { get; set; }

    public FramePadding? Padding { get; set; }

    public static ComponentFrameInput From(ComponentFrame frame, Theme theme)
    {
        var custom = frame is ComponentFrame.Custom chosen ? chosen : null;
        var background = custom?.Background ?? theme.Frame.BackgroundColor(theme.Colors);

        return new()
        {
            Type = frame.Type,
            HasOwnBackground = custom?.Background is not null,
            Background = (background with { Alpha = 255 }).Hex,
            BackgroundOpacityPercent = background.OpacityPercent,
            HasOwnBorder = custom?.Border is not null,
            Border = (custom?.Border ?? theme.Colors.Resolve(theme.Frame.Border)).Hex,
            BorderWidth = custom?.BorderWidth,
            Corners = custom?.Corners,
            Padding = custom?.Padding,
        };
    }

    // null when the post sent something the page couldn't have, or left the type out
    public ComponentFrame? ToFrame()
    {
        var background = HasOwnBackground ? RgbColor.FromPicker(Background)?.WithOpacityPercent(BackgroundOpacityPercent) : null;
        var border = HasOwnBorder ? RgbColor.FromPicker(Border) : null;

        return Type switch
        {
            ComponentFrameType.Off => new ComponentFrame.Off(),
            ComponentFrameType.Theme => new ComponentFrame.Theme(),
            ComponentFrameType.Custom
                when (!HasOwnBackground || background is not null && ThemeFrame.AllowsOpacity(BackgroundOpacityPercent))
                && (!HasOwnBorder || border is not null)
                && IsDefinedOrNull(BorderWidth)
                && IsDefinedOrNull(Corners)
                && IsDefinedOrNull(Padding) => new ComponentFrame.Custom(background, border, BorderWidth, Corners, Padding),
            _ => null,
        };
    }

    private static bool IsDefinedOrNull<TEnum>(TEnum? value)
        where TEnum : struct, Enum => value is not { } chosen || Enum.IsDefined(chosen);
}
