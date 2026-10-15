using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Components.Pages.Admin.TextEditor;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Publishing;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Publishing.Posts;

// form posts create this, and they need exactly one public constructor: with two, mapping
// fails with "does not have a constructor"
public class PostForm : ServerValidatedForm, IValidatableObject
{
    public static PostForm ForNew() => new() { Body = RichText.Empty.Json };

    // LibraryImages holds the library images the body shows, which the editor draws its image embeds from
    public static PostForm FromPost(Post post, IReadOnlyDictionary<string, UploadedImage> libraryImages) =>
        new() { Title = post.Title.Value, Body = LibraryImageFields.Fill(post.Body, libraryImages).Json };

    [Required]
    [StringLength(ArtistShopLimits.PostTitleMaximumLength)]
    public string? Title { get; set; }

    [Required]
    public string? Body { get; set; }

    // Set by the submit button that was pressed, each posting its own value. With none, as when a
    // script submits the form, it stays Draft, so a save can't publish by accident
    public PostStatus Status { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Title is not null && PostSlug.FromTitle(Title).Value.Length is 0)
        {
            yield return new ValidationResult(
                "This title has no letters or numbers to build a web address from.",
                [nameof(Title)]
            );
        }

        if (Body is not null && !RichText.IsDelta(Body))
        {
            yield return new ValidationResult(RichTextChecks.NotInEditorFormat("post"), [nameof(Body)]);
        }
    }

    public void AddTitleTakenError()
    {
        AddServerError(
            nameof(Title),
            "Another post already has this title, or one that only differs in punctuation, accents or capital letters."
        );
    }

    // false when the form says under the text why it can't be saved
    public async Task<bool> CheckBodyAsync(UploadedImageRepository uploadedImageRepository)
    {
        var problems = await RichTextChecks.SaveProblemsAsync(ToPostBody(), uploadedImageRepository, "post");

        foreach (var problem in problems)
        {
            AddServerError(nameof(Body), problem);
        }

        return problems.Count == 0;
    }

    public PostTitle ToPostTitle() => new(Unwrap.Value(Title).Trim());

    public PostSlug ToPostSlug() => PostSlug.FromTitle(Unwrap.Value(Title));

    public RichText ToPostBody() => LibraryImageFields.Strip(new RichText(Unwrap.Value(Body)));
}
