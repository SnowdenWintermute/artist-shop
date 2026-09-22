using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Publishing.Posts;

// form posts create this, and they need exactly one public constructor: with two, mapping
// fails with "does not have a constructor"
public class PostForm : ServerValidatedForm, IValidatableObject
{
    public static PostForm ForNew() => new() { Body = PostBody.Empty.Json };

    public static PostForm FromPost(Post post) =>
        new() { Title = post.Title.Value, Body = post.Body.Json };

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

        if (Body is not null && !PostBody.IsDelta(Body))
        {
            yield return new ValidationResult(
                "The post's text didn't arrive in the editor's format, so nothing was saved.",
                [nameof(Body)]
            );
        }
    }

    public void AddTitleTakenError()
    {
        AddServerError(
            nameof(Title),
            "Another post already has this title, or one that only differs in punctuation, accents or capital letters."
        );
    }

    public PostTitle ToPostTitle() => new(Unwrap.Value(Title).Trim());

    public PostSlug ToPostSlug() => PostSlug.FromTitle(Unwrap.Value(Title));

    public PostBody ToPostBody() => new(Unwrap.Value(Body));
}
