using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.CustomPages;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Components.Pages.Admin.TextEditor;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Publishing;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

// A component's name, and the settings of its type: a text component's text and width, a card grid's source,
// heading and paging, an image's fit, padding and link, a carousel's source and how it shows its slides. Form posts
// create this, so it keeps a single public constructor
public class CustomComponentForm : CollectionChoiceForm, IValidatableObject
{
    [Required]
    [StringLength(ArtistShopLimits.CustomComponentNameMaximumLength)]
    public string? Name { get; set; }

    // a text component's, as the editor writes it; null for the other types
    public string? Body { get; set; }

    public TextWidth Width { get; set; }

    public CardGridSourceType Source { get; set; }

    public CardGridHeadingType HeadingType { get; set; }

    [StringLength(ArtistShopLimits.CardGridHeadingMaximumLength)]
    public string? HeadingText { get; set; }

    [Range(1, ArtistShopLimits.CardGridMaximumCardsPerPage)]
    public int? CardsPerPage { get; set; }

    public bool HasPageLinks { get; set; }

    public CardGridLayout Layout { get; set; }

    public ImageHeight ImageHeight { get; set; }

    public ImageFit Fit { get; set; }

    public ImagePadding Padding { get; set; }

    public ImageLink Link { get; set; }

    public SlideSourceType SlideSource { get; set; }

    public CarouselTransition Transition { get; set; }

    public CarouselAutoplay Autoplay { get; set; }

    public CarouselControls Controls { get; set; }

    public CarouselLink CarouselLink { get; set; }

    public CarouselCaption Caption { get; set; }

    // LibraryImages holds the library images a text component's text shows, which the editor draws its
    // image embeds from
    public static CustomComponentForm From(CustomComponent component, IReadOnlyDictionary<string, UploadedImage> libraryImages) =>
        component.Settings switch
        {
            TextSettings text => new()
            {
                Name = component.Name,
                Body = LibraryImageFields.Fill(text.Body, libraryImages).Json,
                Width = text.Width,
            },
            CardGridSettings cardGrid => new()
            {
                Name = component.Name,
                Source = cardGrid.Source.Type,
                CollectionId = CollectionIdOf(cardGrid.Source),
                HeadingType = cardGrid.Heading.Type,
                HeadingText = cardGrid.Heading is CardGridHeading.Custom custom ? custom.Text : null,
                CardsPerPage = cardGrid.CardsPerPage,
                HasPageLinks = cardGrid.HasPageLinks,
                Layout = cardGrid.Layout,
            },
            ImageSettings image => new()
            {
                Name = component.Name,
                ImageStorageKey = ImageStorageKeyOf(image.Source),
                ImageWorkId = ImageWorkIdOf(image.Source),
                ImageHeight = image.Height,
                Fit = image.Fit,
                Padding = image.Padding,
                Link = image.Link,
            },
            CarouselSettings carousel => new()
            {
                Name = component.Name,
                SlideSource = carousel.Source.Type,
                CollectionId = CollectionIdOf(carousel.Source),
                Transition = carousel.Transition,
                Autoplay = carousel.Autoplay,
                ImageHeight = carousel.Height,
                Fit = carousel.Fit,
                Controls = carousel.Controls,
                CarouselLink = carousel.Link,
                Caption = carousel.Caption,
            },
            _ => new() { Name = component.Name },
        };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Body is not null && !RichText.IsDelta(Body))
        {
            yield return new ValidationResult(RichTextChecks.NotInEditorFormat("component"), [nameof(Body)]);
        }
    }

    public void AddNameTakenError() => AddServerError(nameof(Name), "Another component has this name.");

    // A text component's text, refused as a post's is. A post that left it out, as a script might, is refused
    // too. False when the form says under the text why
    public async Task<bool> CheckBodyAsync(UploadedImageRepository uploadedImageRepository, CustomComponentSettings saved)
    {
        if (saved is not TextSettings)
        {
            return true;
        }

        IReadOnlyList<string> problems = Body is null
            ? [RichTextChecks.NotInEditorFormat("component")]
            : await RichTextChecks.SaveProblemsAsync(new RichText(Body), uploadedImageRepository, "component");

        foreach (var problem in problems)
        {
            AddServerError(nameof(Body), problem);
        }

        return problems.Count == 0;
    }

    // A card grid's choices that depend on each other or on the catalog. False when the form says by the
    // field why
    public bool CheckCardGrid(CustomComponentSettings saved, IReadOnlyList<Collection> collections)
    {
        if (saved is not CardGridSettings)
        {
            return true;
        }

        var isValid = CheckSelects(
            (Enum.IsDefined(Source), nameof(Source)),
            (Enum.IsDefined(HeadingType), nameof(HeadingType)),
            (Enum.IsDefined(Layout), nameof(Layout))
        );

        if (CardsPerPage is null)
        {
            AddServerError(nameof(CardsPerPage), "Say how many cards a page holds.");
            isValid = false;
        }

        if (Source is CardGridSourceType.WorksInCollection && !CheckCollectionChosen(collections))
        {
            isValid = false;
        }

        if (HeadingType is CardGridHeadingType.Custom && string.IsNullOrWhiteSpace(HeadingText))
        {
            AddServerError(nameof(HeadingText), "Write the heading, or choose None.");
            isValid = false;
        }

        return isValid;
    }

    // an image's selects, and its image
    public async Task<bool> CheckImageAsync(
        UploadedImageRepository uploadedImageRepository,
        WorkRepository workRepository,
        SiteWording wording,
        CustomComponentSettings saved
    )
    {
        if (saved is not ImageSettings)
        {
            return true;
        }

        var isValid = CheckSelects(
            (Enum.IsDefined(ImageHeight), nameof(ImageHeight)),
            (Enum.IsDefined(Fit), nameof(Fit)),
            (Enum.IsDefined(Padding), nameof(Padding)),
            (Enum.IsDefined(Link), nameof(Link))
        );

        return await CheckChosenImageAsync(uploadedImageRepository, workRepository, wording) && isValid;
    }

    // A carousel's selects, and its collection. False when the form says by the field why
    public bool CheckCarousel(CustomComponentSettings saved, IReadOnlyList<Collection> collections)
    {
        if (saved is not CarouselSettings)
        {
            return true;
        }

        var isValid = CheckSelects(
            (Enum.IsDefined(SlideSource), nameof(SlideSource)),
            (Enum.IsDefined(Transition), nameof(Transition)),
            (Enum.IsDefined(Autoplay), nameof(Autoplay)),
            (CarouselSettings.Heights.Contains(ImageHeight), nameof(ImageHeight)),
            (Enum.IsDefined(Fit), nameof(Fit)),
            (Enum.IsDefined(Controls), nameof(Controls)),
            (Enum.IsDefined(CarouselLink), nameof(CarouselLink)),
            (Enum.IsDefined(Caption), nameof(Caption))
        );

        if (SlideSource is SlideSourceType.WorksInCollection && !CheckCollectionChosen(collections))
        {
            isValid = false;
        }

        return isValid;
    }

    public string ToName() => Unwrap.Value(Name).Trim();

    // the saved settings with this form's changes; a type with none comes back as it was
    public CustomComponentSettings ToSettings(CustomComponentSettings saved) =>
        saved switch
        {
            TextSettings => new TextSettings(LibraryImageFields.Strip(new RichText(Unwrap.Value(Body))), Width),
            CardGridSettings => new CardGridSettings(
                CardGridSource.Of(Source, ChosenCollectionId),
                // a custom list has nothing to name it by, so Automatic, which a grid had before switching to one, is None
                CardGridHeading.Of(
                    Source is CardGridSourceType.ChosenCards && HeadingType is CardGridHeadingType.Automatic ? CardGridHeadingType.None : HeadingType,
                    HeadingText?.Trim()
                ),
                Unwrap.Value(CardsPerPage), HasPageLinks,
                Layout),
            // a library image has no artwork page, so a link to one, left from a work's image, opens the lightbox
            ImageSettings => new ImageSettings(
                ChosenImageSource,
                ImageHeight,
                Fit,
                Padding,
                Link is ImageLink.ArtworkPage && ChosenImageSource is not ImageSource.Work ? ImageLink.Lightbox : Link
            ),
            CarouselSettings => new CarouselSettings(
                Domain.Website.SlideSource.Of(SlideSource, ChosenCollectionId),
                Transition,
                Autoplay,
                ImageHeight,
                Fit,
                Controls,
                CarouselLink,
                Caption
            ),
            _ => saved,
        };
}
