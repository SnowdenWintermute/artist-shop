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

// A component's name, its frame, and the settings of its type: a text component's text and width, a heading's text, size,
// font, boldness, alignment and link, a button's text, link, alignment, style, size and colour, a card grid's source, heading and paging, an image's fit and link, a carousel's source and how it shows its slides. Form posts
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

    // a card grid's custom heading, or a heading component's text
    [StringLength(ArtistShopLimits.HeadingTextMaximumLength)]
    public string? HeadingText { get; set; }

    public TextSize Size { get; set; }

    public FontRole Font { get; set; }

    public bool Bold { get; set; }

    // a heading's or a button's
    public ComponentAlignment Alignment { get; set; }

    [StringLength(ArtistShopLimits.HeadingTextMaximumLength)]
    public string? ButtonText { get; set; }

    public ButtonStyle ButtonStyle { get; set; }

    public int ButtonSize { get; set; }

    public ButtonColor ButtonColor { get; set; }

    // null for the theme frame's
    public FrameBorderWidth? ButtonBorderWidth { get; set; }

    public FrameCorners? ButtonCorners { get; set; }

    [Range(1, ArtistShopLimits.CardGridMaximumCardsPerPage)]
    public int? CardsPerPage { get; set; }

    public bool HasPageLinks { get; set; }

    public CardGridLayout Layout { get; set; }

    public ImageHeight ImageHeight { get; set; }

    public ImageFit Fit { get; set; }

    public ImageLink Link { get; set; }

    public SlideSourceType SlideSource { get; set; }

    public CarouselTransition Transition { get; set; }

    public CarouselAutoplay Autoplay { get; set; }

    public CarouselControls Controls { get; set; }

    public CarouselLink CarouselLink { get; set; }

    public CarouselCaption Caption { get; set; }

    public ComponentFrameInput Frame { get; set; } = new();

    // A heading's or a button's link, or an image's when its Link is Target. A post that sent none of its fields, as an image
    // linking anywhere else doesn't, binds null, which is no link
    public LinkTargetInput LinkTo
    {
        get => _linkTo;
        set => _linkTo = value ?? new();
    }

    private LinkTargetInput _linkTo = new();

    // LibraryImages holds the library images a text component's text shows, which the editor draws its
    // image embeds from. Theme is the website's, whose frame colours the frame's pickers start from
    public static CustomComponentForm From(
        CustomComponent component,
        IReadOnlyDictionary<string, UploadedImage> libraryImages,
        Theme theme
    )
    {
        var form = FromSettings(component, libraryImages);
        form.Frame = ComponentFrameInput.From(component.Frame, theme);
        form.LinkTo = LinkTargetInput.From(
            component.Settings switch
            {
                HeadingSettings heading => heading.LinkTarget,
                ImageSettings image => image.LinkTarget,
                ButtonSettings button => button.LinkTarget,
                _ => null,
            }
        );

        return form;
    }

    private static CustomComponentForm FromSettings(CustomComponent component, IReadOnlyDictionary<string, UploadedImage> libraryImages) =>
        component.Settings switch
        {
            TextSettings text => new()
            {
                Name = component.Name,
                Body = LibraryImageFields.Fill(RichTextDeltaWriter.Rewrite(text.Body), libraryImages).Json,
                Width = text.Width,
            },
            HeadingSettings heading => new()
            {
                Name = component.Name,
                HeadingText = heading.Text,
                Size = heading.Size,
                Font = heading.Font,
                Bold = heading.Bold,
                Alignment = heading.Alignment,
            },
            ButtonSettings button => new()
            {
                Name = component.Name,
                ButtonText = button.Text,
                Alignment = button.Alignment,
                ButtonStyle = button.Style,
                ButtonSize = button.Size,
                ButtonColor = button.Color,
                ButtonBorderWidth = button.BorderWidth,
                ButtonCorners = button.Corners,
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

    // A heading's text and selects. False when the form says by the field why
    public bool CheckHeading(CustomComponentSettings saved)
    {
        if (saved is not HeadingSettings)
        {
            return true;
        }

        var isValid = CheckSelects(
            (Enum.IsDefined(Size), nameof(Size)),
            (Enum.IsDefined(Font), nameof(Font)),
            (Enum.IsDefined(Alignment), nameof(Alignment))
        );

        if (string.IsNullOrWhiteSpace(HeadingText))
        {
            AddServerError(nameof(HeadingText), "Write the heading.");
            isValid = false;
        }

        return isValid;
    }

    // A button's text and choices; its link is CheckLink's. False when the form says by the field why
    public bool CheckButton(CustomComponentSettings saved)
    {
        if (saved is not ButtonSettings)
        {
            return true;
        }

        var isValid = CheckSelects(
            (Enum.IsDefined(Alignment), nameof(Alignment)),
            (Enum.IsDefined(ButtonStyle), nameof(ButtonStyle)),
            (ButtonSettings.AllowsSize(ButtonSize), nameof(ButtonSize)),
            (Enum.IsDefined(ButtonColor), nameof(ButtonColor)),
            (ButtonBorderWidth is not { } width || Enum.IsDefined(width), nameof(ButtonBorderWidth)),
            (ButtonCorners is not { } corners || Enum.IsDefined(corners), nameof(ButtonCorners))
        );

        if (string.IsNullOrWhiteSpace(ButtonText))
        {
            AddServerError(nameof(ButtonText), "Write the button's text.");
            isValid = false;
        }

        return isValid;
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

    // A heading's link, which may be none, a button's, which can't, or an image's set to link to a target. False
    // when the form says by the link why
    public bool CheckLink(CustomComponentSettings saved)
    {
        var problem = saved switch
        {
            HeadingSettings => LinkTo.Problem(),
            ImageSettings when Link is ImageLink.Target => LinkTo.Type is null ? "Choose what to link to." : LinkTo.Problem(),
            ButtonSettings => LinkTo.Type is null ? "Choose what to link to." : LinkTo.Problem(),
            _ => null,
        };

        if (problem is null)
        {
            return true;
        }

        AddServerError(nameof(LinkTo), problem);
        return false;
    }

    // False when the form says by the frame why. A type without frames posts none
    public bool CheckFrame(CustomComponentSettings saved)
    {
        if (!saved.HasFrame || Frame.ToFrame() is not null)
        {
            return true;
        }

        AddServerError(nameof(Frame), "The frame sent isn't one this page could have picked. Reload it and try again.");
        return false;
    }

    public string ToName() => Unwrap.Value(Name).Trim();

    public ComponentFrame ToFrame(CustomComponentSettings saved) =>
        saved.HasFrame ? Frame.ToFrame() ?? throw new InvalidOperationException("Save checks the frame first.") : new ComponentFrame.Off();

    // the saved settings with this form's changes; a type with none comes back as it was
    public CustomComponentSettings ToSettings(CustomComponentSettings saved) =>
        saved switch
        {
            TextSettings => new TextSettings(LibraryImageFields.Strip(new RichText(Unwrap.Value(Body))), Width),
            HeadingSettings => new HeadingSettings(Unwrap.Value(HeadingText).Trim(), Size, Font, Bold, Alignment, LinkTo.ToTarget()),
            ButtonSettings => new ButtonSettings(
                Unwrap.Value(ButtonText).Trim(),
                LinkTo.ToTarget() ?? throw new InvalidOperationException("Save checks the button's link first."),
                Alignment,
                ButtonStyle,
                ButtonSize,
                ButtonColor,
                ButtonBorderWidth,
                ButtonCorners
            ),
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
                Link is ImageLink.ArtworkPage && ChosenImageSource is not ImageSource.Work ? ImageLink.Lightbox : Link,
                Link is ImageLink.Target ? LinkTo.ToTarget() : null
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
