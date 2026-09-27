using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using ArtistShop.Web.Identity;

namespace ArtistShop.Web.Components.Account;

// Checks, every minute an interactive circuit is connected, that its sign-in hasn't been ended since
// the page loaded (DatabaseTicketStore), which includes the account's security stamp changing;
// ordinary requests check that on every request
internal sealed class IdentityRevalidatingAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        DatabaseTicketStore ticketStore)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken) =>
        ticketStore.IsLiveAsync(authenticationState.User);
}
