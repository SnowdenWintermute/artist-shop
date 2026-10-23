namespace ArtistShop.Web.Domain.Website;

// The ids are only for ordering; settings store the names
public enum ComponentFrameType
{
    Off = 1,
    Theme = 2,
    Custom = 3,
}

// The background, border and padding around a component: none, the theme's frame (ThemeFrame), or the theme's
// with some of it chosen here. Every component has one, Off for a type without frames (HasFrame)
public abstract record ComponentFrame
{
    public abstract ComponentFrameType Type { get; }

    public sealed record Off : ComponentFrame
    {
        public override ComponentFrameType Type => ComponentFrameType.Off;
    }

    public sealed record Theme : ComponentFrame
    {
        public override ComponentFrameType Type => ComponentFrameType.Theme;
    }

    // Each part null takes the theme's, so a change to the theme still reaches it. The colours are any, the
    // background with an opacity of its own
    public sealed record Custom(
        RgbColor? Background,
        RgbColor? Border,
        FrameBorderWidth? BorderWidth,
        FrameCorners? Corners,
        FramePadding? Padding
    ) : ComponentFrame
    {
        public override ComponentFrameType Type => ComponentFrameType.Custom;
    }
}
