using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
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

    // a hex colour, posted only while the background is a colour, or pictures with a colour behind them
    public string? BackgroundColor { get; set; }

    // posted only while the background is an image or a slideshow
    public ImageFit BackgroundFit { get; set; }

    // whether an image or slideshow has a colour behind it rather than the row's page colour; posted only while
    // the background is one
    public bool? HasColorBehindImage { get; set; }

    // posted only while the background is a slideshow; the collection only while it shows one
    public SlideSourceType SlideSource { get; set; }

    public int? CollectionId { get; set; }

    public SlideshowInterval SlideInterval { get; set; }

    public ThemeKey? ThemeKey => Domain.Website.ThemeKey.FromQueryValue(Theme);

    public static RowSettingsForm From(RowSettings settings)
    {
        var image = settings.Background is RowBackground.Image imageBackground ? imageBackground : null;
        var slideshow = settings.Background is RowBackground.Slideshow slideshowBackground ? slideshowBackground : null;

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
            BackgroundFit = settings.Background.PictureFit ?? ImageFit.Cover,
            HasColorBehindImage = (image?.Behind ?? slideshow?.Behind) is not null,
            SlideSource = slideshow?.Source.Type ?? SlideSourceType.ChosenSlides,
            CollectionId = slideshow?.Source is Domain.Website.SlideSource.WorksInCollection inCollection ? inCollection.CollectionId.Value : null,
            SlideInterval = slideshow?.Interval ?? SlideshowInterval.TenSeconds,
            ImageStorageKey = ImageStorageKeyOf(image?.Source),
            ImageWorkId = ImageWorkIdOf(image?.Source),
        };
    }

    // collections is what a slideshow can show the works of
    public async Task<bool> CheckAsync(
        UploadedImageRepository uploadedImageRepository,
        WorkRepository workRepository,
        SiteWording wording,
        IReadOnlyList<Collection> collections
    )
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

        if (BackgroundType is RowBackgroundType.Slideshow && SlideSource is SlideSourceType.WorksInCollection
            && !collections.Any(collection => collection.Id.Value == CollectionId))
        {
            AddServerError(nameof(CollectionId), "Choose one.");
            isValid = false;
        }

        return BackgroundType is RowBackgroundType.Image
            ? await CheckChosenImageAsync(uploadedImageRepository, workRepository, wording) && isValid
            : isValid;
    }

    // A choice made just now has nothing of its own posted, so nothing changes until one is: a colour
    // starts as the row's page colour; an image's or slideshow's fit as Cover, and the colour behind it as the
    // colour background it replaces, if it was one; a slideshow's slides as chosen ones, every ten seconds
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
                RowBackgroundType.Image => new RowBackground.Image(ChosenImageSource, PostedFit, BehindPictures(postedColor, pageColor)),
                RowBackgroundType.Slideshow => new RowBackground.Slideshow(
                    Enum.IsDefined(SlideSource)
                        ? Domain.Website.SlideSource.Of(SlideSource, CollectionId is int collectionId ? new CollectionId(collectionId) : null)
                        : new Domain.Website.SlideSource.ChosenSlides(),
                    Enum.IsDefined(SlideInterval) ? SlideInterval : SlideshowInterval.TenSeconds,
                    PostedFit,
                    BehindPictures(postedColor, pageColor)
                ),
                _ => throw new InvalidOperationException($"{BackgroundType} isn't a background."),
            }
        );
    }

    private ImageFit PostedFit => Enum.IsDefined(BackgroundFit) ? BackgroundFit : ImageFit.Cover;

    private RgbColor? BehindPictures(RgbColor? postedColor, RgbColor pageColor) =>
        HasColorBehindImage switch
        {
            false => null,
            true => postedColor ?? pageColor,
            null => postedColor,
        };
}
