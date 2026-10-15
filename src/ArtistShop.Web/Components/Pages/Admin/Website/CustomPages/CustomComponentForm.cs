using System.ComponentModel.DataAnnotations;
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
// heading and paging. Form posts create this, so it keeps a single public constructor
public class CustomComponentForm : ServerValidatedForm, IValidatableObject
{
    [Required]
    [StringLength(ArtistShopLimits.CustomComponentNameMaximumLength)]
    public string? Name { get; set; }

    // a text component's, as the editor writes it; null for the other types
    public string? Body { get; set; }

    public TextWidth Width { get; set; }

    public CardGridSourceType Source { get; set; }

    // the collection whose works a card grid shows
    public int? CollectionId { get; set; }

    public CardGridHeadingType HeadingType { get; set; }

    [StringLength(ArtistShopLimits.CardGridHeadingMaximumLength)]
    public string? HeadingText { get; set; }

    [Range(1, ArtistShopLimits.CardGridMaximumCardsPerPage)]
    public int? CardsPerPage { get; set; }

    public bool HasPageLinks { get; set; }

    public CardGridLayout Layout { get; set; }

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
                CollectionId = cardGrid.Source is CardGridSource.WorksInCollection inCollection
                    ? inCollection.CollectionId.Value
                    : null,
                HeadingType = cardGrid.Heading.Type,
                HeadingText = cardGrid.Heading is CardGridHeading.Custom custom ? custom.Text : null,
                CardsPerPage = cardGrid.CardsPerPage,
                HasPageLinks = cardGrid.HasPageLinks,
                Layout = cardGrid.Layout,
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

        var isValid = true;

        // a post that left a select out, as a script might, would otherwise save its type's default of 0
        foreach (var (isDefined, field) in new[]
        {
            (Enum.IsDefined(Source), nameof(Source)),
            (Enum.IsDefined(HeadingType), nameof(HeadingType)),
            (Enum.IsDefined(Layout), nameof(Layout)),
        })
        {
            if (!isDefined)
            {
                AddServerError(field, "Choose one.");
                isValid = false;
            }
        }

        if (CardsPerPage is null)
        {
            AddServerError(nameof(CardsPerPage), "Say how many cards a page holds.");
            isValid = false;
        }

        if (Source is CardGridSourceType.WorksInCollection && !collections.Any(collection => collection.Id.Value == CollectionId))
        {
            AddServerError(nameof(CollectionId), "Choose one.");
            isValid = false;
        }

        if (HeadingType is CardGridHeadingType.Custom && string.IsNullOrWhiteSpace(HeadingText))
        {
            AddServerError(nameof(HeadingText), "Write the heading, or choose None.");
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
                CardGridSource.Of(Source, CollectionId is int collectionId ? new CollectionId(collectionId) : null),
                // a custom list has nothing to name it by, so Automatic, which a grid had before switching to one, is None
                CardGridHeading.Of(
                    Source is CardGridSourceType.ChosenCards && HeadingType is CardGridHeadingType.Automatic ? CardGridHeadingType.None : HeadingType,
                    HeadingText?.Trim()
                ),
                Unwrap.Value(CardsPerPage), HasPageLinks,
                Layout),
            _ => saved,
        };
}
