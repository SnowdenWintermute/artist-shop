namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Database.Repositories;

// Sign-up codes and admin invitations a while after they expire. Until then the operator and owners
// see them marked expired, so one doesn't just vanish, and can delete them by hand
public sealed class ExpiredRowCleanup(
    SignUpCodeRepository signUpCodeRepository,
    SiteInviteRepository siteInviteRepository,
    TimeProvider timeProvider
)
{
    public static readonly TimeSpan KeptAfterExpiring = TimeSpan.FromDays(30);

    public async Task DeleteAsync()
    {
        var before = timeProvider.GetUtcNow() - KeptAfterExpiring;

        await signUpCodeRepository.DeleteExpiredBeforeAsync(before);
        await siteInviteRepository.DeleteExpiredBeforeAsync(before);
    }
}
