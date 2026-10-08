using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Components.Pages.Admin.Website;

// a blank word is the default; form posts create this, so it keeps a single public constructor
public class WordingForm : IValidatableObject
{
    [StringLength(ArtistShopLimits.WordingWordMaximumLength)]
    public string? CollectionSingular { get; set; }

    [StringLength(ArtistShopLimits.WordingWordMaximumLength)]
    public string? CollectionPlural { get; set; }

    public bool CollectionKeepsCase { get; set; }

    [StringLength(ArtistShopLimits.WordingWordMaximumLength)]
    public string? WorkSingular { get; set; }

    [StringLength(ArtistShopLimits.WordingWordMaximumLength)]
    public string? WorkPlural { get; set; }

    public bool WorkKeepsCase { get; set; }

    public static WordingForm From(SiteWording wording) =>
        new()
        {
            CollectionSingular = wording.CollectionChoice.Singular,
            CollectionPlural = wording.CollectionChoice.Plural,
            CollectionKeepsCase = wording.CollectionChoice.KeepsCase,
            WorkSingular = wording.WorkChoice.Singular,
            WorkPlural = wording.WorkChoice.Plural,
            WorkKeepsCase = wording.WorkChoice.KeepsCase,
        };

    public SiteWording ToWording() =>
        new(
            new NounChoice(Word(CollectionSingular), Word(CollectionPlural), CollectionKeepsCase),
            new NounChoice(Word(WorkSingular), Word(WorkPlural), WorkKeepsCase)
        );

    // both forms or neither, so the website never has to guess a plural
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in BothOrNeither(CollectionSingular, nameof(CollectionSingular), CollectionPlural, nameof(CollectionPlural)))
        {
            yield return result;
        }

        foreach (var result in BothOrNeither(WorkSingular, nameof(WorkSingular), WorkPlural, nameof(WorkPlural)))
        {
            yield return result;
        }
    }

    private static IEnumerable<ValidationResult> BothOrNeither(string? singular, string singularName, string? plural, string pluralName)
    {
        if (Word(singular) is null && Word(plural) is not null)
        {
            yield return new ValidationResult("Fill in the singular too, or leave both blank for the default.", [singularName]);
        }
        else if (Word(singular) is not null && Word(plural) is null)
        {
            yield return new ValidationResult("Fill in the plural too, or leave both blank for the default.", [pluralName]);
        }
    }

    private static string? Word(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
