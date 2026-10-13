using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Components.Pages.Admin.TextEditor;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Images;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

// A component's name, and the settings of its type: an article's text and width. Form posts create
// this, so it keeps a single public constructor
public class CustomComponentForm : ServerValidatedForm, IValidatableObject
{
    [Required]
    [StringLength(ArtistShopLimits.CustomComponentNameMaximumLength)]
    public string? Name { get; set; }

    // an article's, as the post editor writes it; null for the other types
    public string? Body { get; set; }

    public ArticleWidth Width { get; set; }

    public static CustomComponentForm From(CustomComponent component) =>
        component.Settings switch
        {
            ArticleSettings article => new()
            {
                Name = component.Name,
                Body = article.Body.Json,
                Width = article.Width,
            },
            _ => new() { Name = component.Name },
        };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Body is not null && !PostBody.IsDelta(Body))
        {
            yield return new ValidationResult(PostBodyChecks.NotInEditorFormat("article"), [nameof(Body)]);
        }
    }

    public void AddNameTakenError() => AddServerError(nameof(Name), "Another component has this name.");

    // An article's text, refused as a post's is. A post that left it out, as a script might, is refused
    // too. False when the form says under the text why
    public bool CheckBody(ImageStorage imageStorage, CustomComponentSettings saved)
    {
        if (saved is not ArticleSettings)
        {
            return true;
        }

        IReadOnlyList<string> problems = Body is null
            ? [PostBodyChecks.NotInEditorFormat("article")]
            : PostBodyChecks.SaveProblems(new PostBody(Body), imageStorage, "article");

        foreach (var problem in problems)
        {
            AddServerError(nameof(Body), problem);
        }

        return problems.Count == 0;
    }

    public string ToName() => Unwrap.Value(Name).Trim();

    // the saved settings with this form's changes; a type with none comes back as it was
    public CustomComponentSettings ToSettings(CustomComponentSettings saved) =>
        saved switch
        {
            ArticleSettings => new ArticleSettings(new PostBody(Unwrap.Value(Body)), Width),
            _ => saved,
        };
}
