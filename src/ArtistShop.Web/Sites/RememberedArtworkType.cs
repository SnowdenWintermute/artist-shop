using System.Globalization;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Components.Authorization;

namespace ArtistShop.Web.Sites;

// The artwork type the signed-in admin last chose in any artwork type select, which the others start
// at. Reads the user from the authentication state, so it works on static pages and in islands alike
public class RememberedArtworkType(
    SiteMemberInputValueRepository inputValues,
    CurrentSite currentSite,
    AuthenticationStateProvider authenticationStateProvider
)
{
    // null when they never chose one, or it has since been deleted
    public async Task<ArtworkType?> FindInAsync(IReadOnlyList<ArtworkType> types)
    {
        var saved = await inputValues.GetAsync(currentSite.Id, await UserIdAsync(), RememberedInputs.ArtworkType);
        return types.FirstOrDefault(type => ValueOf(type.Id) == saved);
    }

    public async Task SetAsync(ArtworkTypeId id) =>
        await inputValues.SetAsync(currentSite.Id, await UserIdAsync(), RememberedInputs.ArtworkType, ValueOf(id));

    // as a RememberedSelect's option submits it
    public static string ValueOf(ArtworkTypeId id) => id.Value.ToString(CultureInfo.InvariantCulture);

    private async Task<string> UserIdAsync() =>
        (await authenticationStateProvider.GetAuthenticationStateAsync()).User.RequiredUserId();
}
