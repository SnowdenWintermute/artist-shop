namespace ArtistShop.Web.Sites;

using System.Security.Claims;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

// RememberedSelect's script saves an admin's change here, for the page to show when it next renders.
// The form fields make it check the antiforgery token, as an EditForm's post does
public static class RememberedInputEndpoints
{
    public const string Path = "/admin/remembered-inputs";

    // the form field names the script sends
    public const string NameField = "name";
    public const string ValueField = "value";

    public static void MapRememberedInputEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(Path, SetAsync)
            .RequireAuthorization(SitePolicies.Admin)
            .WithMetadata(new ServedOnAttribute(HostTypes.Site));

    private static async Task<Results<NoContent, BadRequest>> SetAsync(
        [FromForm(Name = NameField)] string inputName,
        [FromForm(Name = ValueField)] string value,
        ClaimsPrincipal user,
        CurrentSite currentSite,
        SiteMemberInputValueRepository inputValues
    )
    {
        if (!RememberedInputs.Names.Contains(inputName) || value.Length > ArtistShopLimits.RememberedInputValueMaximumLength)
        {
            return TypedResults.BadRequest();
        }

        await inputValues.SetAsync(currentSite.Id, user.RequiredUserId(), inputName, value);

        return TypedResults.NoContent();
    }
}
