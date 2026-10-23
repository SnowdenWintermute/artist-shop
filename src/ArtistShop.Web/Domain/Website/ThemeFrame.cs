namespace ArtistShop.Web.Domain.Website;

// how thick a frame's border is. The ids are only for ordering; settings store the names
public enum FrameBorderWidth
{
    None = 1,
    Thin = 2,
    Medium = 3,
    Thick = 4,
}

// how round a frame's corners are. The ids are only for ordering; settings store the names
public enum FrameCorners
{
    None = 1,
    Small = 2,
    Medium = 3,
    Large = 4,
}

// the space inside a frame around what it holds. The ids are only for ordering; settings store the names
public enum FramePadding
{
    None = 1,
    Small = 2,
    Medium = 3,
    Large = 4,
}

// The background, border and padding a component framed by the theme has. The colours are roles, so a
// frame follows the theme's colours, and the background is only one ordinary text reads on. The opacity
// lets a row's image show through
public sealed record ThemeFrame(
    ColorRole Background,
    int BackgroundOpacityPercent,
    ColorRole Border,
    FrameBorderWidth BorderWidth,
    FrameCorners Corners,
    FramePadding Padding
)
{
    public static readonly IReadOnlyList<ColorRole> BackgroundRoles = [ColorRole.Panel, ColorRole.Page, ColorRole.Bar];

    public static readonly IReadOnlyList<ColorRole> BorderRoles = [ColorRole.Rule, ColorRole.Ink, ColorRole.Accent];

    public static bool AllowsOpacity(int percent) => percent is >= 0 and <= 100;

    // its background as drawn, the role's colour at the frame's opacity
    public RgbColor BackgroundColor(ThemeColors colors) =>
        colors.Resolve(Background).WithOpacityPercent(BackgroundOpacityPercent);
}
