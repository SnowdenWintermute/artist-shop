using System.Security.Claims;

namespace ArtistShop.Web.Identity;

public static class SignedInUser
{
    // Identity's id for the signed-in account, which Identity keeps in this claim. Only for pages
    // behind [Authorize], where there is always one
    public static string RequiredUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("A signed-in account has no id.");
}
