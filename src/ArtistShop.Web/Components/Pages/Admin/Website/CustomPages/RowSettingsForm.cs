using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

// The row editor's settings: everything about a row but its slots, saved whole on every change. Form
// posts create this, so it keeps a single public constructor
public class RowSettingsForm : ServerValidatedForm
{
    // the Theme radio for the website's own, which no ThemeKey.QueryValue can be
    public const string WebsiteTheme = "website";

    public RowHeight Height { get; set; }

    public RowContentWidth ContentWidth { get; set; }

    public RowVerticalPadding TopPadding { get; set; }

    public RowVerticalPadding BottomPadding { get; set; }

    public RowSidePadding SidePadding { get; set; }

    public RowGap Gap { get; set; }

    // a ThemeKey.QueryValue, or WebsiteTheme
    public string? Theme { get; set; }

    public RowBackgroundType BackgroundType { get; set; }

    // a hex colour, posted only while the background is a colour
    public string? BackgroundColor { get; set; }

    public ThemeKey? ThemeKey => Domain.Website.ThemeKey.FromQueryValue(Theme);

    public static RowSettingsForm From(RowSettings settings) =>
        new()
        {
            Height = settings.Height,
            ContentWidth = settings.ContentWidth,
            TopPadding = settings.Padding.Top,
            BottomPadding = settings.Padding.Bottom,
            SidePadding = settings.Padding.Sides,
            Gap = settings.Gap,
            Theme = settings.Theme?.QueryValue ?? WebsiteTheme,
            BackgroundType = settings.Background.Type,
            BackgroundColor = settings.Background is RowBackground.Color color ? color.Value.Hex : null,
        };

    public bool Check() =>
        CheckSelects(
            (Enum.IsDefined(Height), nameof(Height)),
            (Enum.IsDefined(ContentWidth), nameof(ContentWidth)),
            (Enum.IsDefined(TopPadding), nameof(TopPadding)),
            (Enum.IsDefined(BottomPadding), nameof(BottomPadding)),
            (Enum.IsDefined(SidePadding), nameof(SidePadding)),
            (Enum.IsDefined(Gap), nameof(Gap)),
            (Enum.IsDefined(BackgroundType), nameof(BackgroundType))
        );

    // a colour chosen just now has none posted, and starts as the row's page colour, so nothing changes
    // until one is picked
    public RowSettings ToSettings(RgbColor pageColor) =>
        new(
            Height,
            ContentWidth,
            new RowPadding(TopPadding, BottomPadding, SidePadding),
            Gap,
            ThemeKey,
            BackgroundType switch
            {
                RowBackgroundType.None => new RowBackground.None(),
                RowBackgroundType.Color => new RowBackground.Color(RgbColor.TryParse(BackgroundColor, out var color) ? color : pageColor),
                _ => throw new InvalidOperationException($"{BackgroundType} isn't a background."),
            }
        );
}
