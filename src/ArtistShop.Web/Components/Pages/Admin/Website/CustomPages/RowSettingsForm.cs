using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

// The row editor's settings: everything about a row but its slots, saved whole on every change. Form
// posts create this, so it keeps a single public constructor
public class RowSettingsForm : ImagePickerForm
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

    // a hex colour, posted only while the background is a colour, or an image with a colour behind it
    public string? BackgroundColor { get; set; }

    // posted only while the background is an image
    public ImageFit BackgroundFit { get; set; }

    // whether an image has a colour behind it rather than the row's page colour; posted only while the
    // background is an image
    public bool? HasColorBehindImage { get; set; }

    public ThemeKey? ThemeKey => Domain.Website.ThemeKey.FromQueryValue(Theme);

    public static RowSettingsForm From(RowSettings settings)
    {
        var image = settings.Background is RowBackground.Image imageBackground ? imageBackground : null;

        return new()
        {
            Height = settings.Height,
            ContentWidth = settings.ContentWidth,
            TopPadding = settings.Padding.Top,
            BottomPadding = settings.Padding.Bottom,
            SidePadding = settings.Padding.Sides,
            Gap = settings.Gap,
            Theme = settings.Theme?.QueryValue ?? WebsiteTheme,
            BackgroundType = settings.Background.Type,
            BackgroundColor = settings.Background.DrawnColor?.Hex,
            BackgroundFit = image?.Fit ?? ImageFit.Cover,
            HasColorBehindImage = image?.Behind is not null,
            ImageStorageKey = ImageStorageKeyOf(image?.Source),
            ImageWorkId = ImageWorkIdOf(image?.Source),
        };
    }

    public async Task<bool> CheckAsync(UploadedImageRepository uploadedImageRepository, WorkRepository workRepository, SiteWording wording)
    {
        var isValid = CheckSelects(
            (Enum.IsDefined(Height), nameof(Height)),
            (Enum.IsDefined(ContentWidth), nameof(ContentWidth)),
            (Enum.IsDefined(TopPadding), nameof(TopPadding)),
            (Enum.IsDefined(BottomPadding), nameof(BottomPadding)),
            (Enum.IsDefined(SidePadding), nameof(SidePadding)),
            (Enum.IsDefined(Gap), nameof(Gap)),
            (Enum.IsDefined(BackgroundType), nameof(BackgroundType))
        );

        // posted only while its picker shows, and empty for none, as ToSettings reads it
        if (!string.IsNullOrEmpty(BackgroundColor) && RgbColor.FromPicker(BackgroundColor) is null)
        {
            AddServerError(nameof(BackgroundColor), "The colour sent isn't one this page could have picked. Reload it and try again.");
            isValid = false;
        }

        return BackgroundType is RowBackgroundType.Image
            ? await CheckChosenImageAsync(uploadedImageRepository, workRepository, wording) && isValid
            : isValid;
    }

    // A choice made just now has nothing of its own posted, so nothing changes until one is: a colour
    // starts as the row's page colour; an image's fit as Cover, and the colour behind it as the colour
    // background it replaces, if it was one
    public RowSettings ToSettings(RgbColor pageColor)
    {
        var postedColor = RgbColor.FromPicker(BackgroundColor);

        return new(
            Height,
            ContentWidth,
            new RowPadding(TopPadding, BottomPadding, SidePadding),
            Gap,
            ThemeKey,
            BackgroundType switch
            {
                RowBackgroundType.None => new RowBackground.None(),
                RowBackgroundType.Color => new RowBackground.Color(postedColor ?? pageColor),
                RowBackgroundType.Image => new RowBackground.Image(
                    ChosenImageSource,
                    Enum.IsDefined(BackgroundFit) ? BackgroundFit : ImageFit.Cover,
                    HasColorBehindImage switch
                    {
                        false => null,
                        true => postedColor ?? pageColor,
                        null => postedColor,
                    }
                ),
                _ => throw new InvalidOperationException($"{BackgroundType} isn't a background."),
            }
        );
    }
}
