namespace ArtistShop.Web.Sites;

using System.Security.Claims;
using ArtistShop.Web.Components.Hints;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

// Hint's script dismisses a hint here, or shows a collapsing one again. The form fields make it check the antiforgery
// token, as an EditForm's post does
public static class DismissedHintEndpoints
{
    public const string Path = "/admin/dismissed-hints";

    // a HintType's name
    public const string HintField = "hint";

    // "true" to dismiss it, "false" to show it again
    public const string DismissedField = "dismissed";

    public static void MapDismissedHintEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(Path, SetAsync)
            .RequireAuthorization(SitePolicies.Admin)
            .WithMetadata(new ServedOnAttribute(HostTypes.Site));

    private static async Task<Results<NoContent, BadRequest>> SetAsync(
        [FromForm(Name = HintField)] string hint,
        [FromForm(Name = DismissedField)] bool dismissed,
        ClaimsPrincipal user,
        DismissedHintRepository dismissedHints
    )
    {
        // only a name, as Enum.TryParse would also take a number
        if (!Enum.GetNames<HintType>().Contains(hint))
        {
            return TypedResults.BadRequest();
        }

        HintType[] hints = [Enum.Parse<HintType>(hint)];

        if (dismissed)
        {
            await dismissedHints.DismissAsync(user.RequiredUserId(), hints);
        }
        else
        {
            await dismissedHints.ShowAsync(user.RequiredUserId(), hints);
        }

        return TypedResults.NoContent();
    }
}
