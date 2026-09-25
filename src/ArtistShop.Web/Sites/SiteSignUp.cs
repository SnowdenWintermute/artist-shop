namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Platform;
using ArtistShop.Web.Domain.Sites;

// what SiteSignUp.SignUpAsync did
public abstract record SiteSignUpResult
{
    // only the ones below
    private SiteSignUpResult() { }

    public sealed record Made(SiteId SiteId, HostName Host) : SiteSignUpResult;

    // never made, expired, used or revoked
    public sealed record CodeNotUsable : SiteSignUpResult;

    public sealed record NameTaken : SiteSignUpResult;
}

// A new website from a sign-up code, owned by the signed-in account that asked for it
public sealed class SiteSignUp(
    SiteRepository sites,
    SiteProvisioner provisioner,
    HostDirectory hostDirectory,
    PlatformSettings platformSettings
)
{
    // ownerUserId is Identity's id for the signed-in account
    public async Task<SiteSignUpResult> SignUpAsync(SignUpCode code, SiteName name, string ownerUserId)
    {
        var host = name.HostUnder(platformSettings.Host);
        SiteId siteId;

        try
        {
            siteId = await sites.AddWithSignUpCodeAsync(code, host, ownerUserId);
        }
        catch (SignUpCodeNotUsableException)
        {
            return new SiteSignUpResult.CodeNotUsable();
        }
        catch (NameAlreadyInUseException)
        {
            return new SiteSignUpResult.NameTaken();
        }

        // if this fails, the site is finished at the app's next start, which prepares every site
        provisioner.Prepare(siteId);
        await hostDirectory.ReloadAsync();

        return new SiteSignUpResult.Made(siteId, host);
    }
}
