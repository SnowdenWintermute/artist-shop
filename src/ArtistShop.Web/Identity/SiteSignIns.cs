namespace ArtistShop.Web.Identity;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArtistShop.Web.Components;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

// the account a website's handoff signs in, the platform sign-in it becomes part of, and where to go
// afterwards
public sealed record SiteSignInHandoff(ApplicationUser Account, bool IsPersistent, string PlatformSessionId, string? ReturnUrl);

// Signing in on a website happens on the platform, the one host with a sign-in page, since each host
// has its own cookie and Google returns only to the platform. In three steps:
// 1. The website (Start) keeps a new nonce and the page to come back to in a cookie on its own host,
//    and sends the browser to the platform's /Account/SignInTo with the nonce.
// 2. The platform, once the browser is signed in there, makes a single-use code for that website
//    and nonce (MakeCodeAsync) and sends the browser to the website's /Account/Handoff with it.
// 3. The website uses the code up (UseCodeAsync): it must be for this host, unexpired, and carry the
//    nonce in this browser's cookie, and the platform sign-in that made it must not have ended since.
//    The nonce check stops someone signing a victim's browser in to the someone's own account with a
//    code they made: their code carries their nonce, not the victim's
public sealed class SiteSignIns(
    ApplicationDbContext database,
    UserManager<ApplicationUser> userManager,
    HostDirectory hostDirectory,
    DatabaseTicketStore ticketStore,
    IDataProtectionProvider dataProtection,
    TimeProvider timeProvider
)
{
    // the trip from the platform back to the website is one redirect
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(1);

    // time to sign in on the platform, a Google trip included
    private static readonly TimeSpan StartLifetime = TimeSpan.FromMinutes(15);

    private const string CookieName = "ArtistShop.SiteSignIn";

    // what the website's cookie holds between steps 1 and 3
    private sealed record Started(string Nonce, string? ReturnUrl);

    // step 1, on a website: the platform's address to send the browser to
    public string Start(HttpContext context, string? returnUrl, string siteBaseUri, HostName platformHost)
    {
        var nonce = RandomToken();
        var started = JsonSerializer.Serialize(new Started(nonce, LocalUrl.OrNull(returnUrl)));

        context.Response.Cookies.Append(CookieName, Protector().Protect(started, StartLifetime), CookieOptions(context));

        return PageUrls.PlatformSiteSignIn(siteBaseUri, platformHost, context.Request.Host.Host, nonce);
    }

    // step 2, on the platform: the code, or null when siteHost is no website's host
    public async Task<string?> MakeCodeAsync(
        string userId,
        string platformSessionId,
        string siteHost,
        string nonce,
        bool isPersistent
    )
    {
        if (hostDirectory.Find(siteHost) is not CurrentHost.Site || HostName.Read(siteHost) is not { } host)
        {
            return null;
        }

        var code = RandomToken();

        database.SiteSignInCodes.Add(
            new SiteSignInCode
            {
                CodeHash = Hash(code),
                UserId = userId,
                SiteHost = host.Value,
                Nonce = nonce,
                IsPersistent = isPersistent,
                PlatformSessionId = platformSessionId,
                ExpiresAt = timeProvider.GetUtcNow() + CodeLifetime,
            }
        );
        await database.SaveChangesAsync();

        return code;
    }

    // Step 3, on the website: null when any check fails. Takes the cookie either way, so a failed
    // try starts over clean. A platform sign-in ended since the code was made (logged out, or a new
    // security stamp) would otherwise live on in the website's; a deleted one would fail the website
    // row's foreign key
    public async Task<SiteSignInHandoff?> UseCodeAsync(HttpContext context, string? code)
    {
        var started = TakeStarted(context);

        if (started is null || string.IsNullOrEmpty(code) || HostName.Read(context.Request.Host.Host) is not { } host)
        {
            return null;
        }

        var hash = Hash(code);
        var row = await database.SiteSignInCodes.AsNoTracking().SingleOrDefaultAsync(row => row.CodeHash == hash);

        // deleted before anything is checked, so a code is used once even by two requests at once:
        // only the one whose delete took the row goes on
        if (row is null || await database.SiteSignInCodes.Where(row => row.CodeHash == hash).ExecuteDeleteAsync() == 0)
        {
            return null;
        }

        var isValid =
            row.ExpiresAt > timeProvider.GetUtcNow()
            && row.SiteHost == host.Value
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(row.Nonce), Encoding.UTF8.GetBytes(started.Nonce));

        if (
            !isValid
            || !await ticketStore.IsLiveAsync(row.PlatformSessionId)
            || await userManager.FindByIdAsync(row.UserId) is not { } account
        )
        {
            return null;
        }

        return new SiteSignInHandoff(account, row.IsPersistent, row.PlatformSessionId, started.ReturnUrl);
    }

    // by the daily cleanup
    public async Task DeleteExpiredAsync()
    {
        var now = timeProvider.GetUtcNow();
        await database.SiteSignInCodes.Where(row => row.ExpiresAt <= now).ExecuteDeleteAsync();
    }

    private Started? TakeStarted(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var value))
        {
            return null;
        }

        context.Response.Cookies.Delete(CookieName, CookieOptions(context));

        try
        {
            return JsonSerializer.Deserialize<Started>(Protector().Unprotect(value));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    // Only sent to the handoff. Lax, not Strict: the browser arrives there from the platform, another
    // site, and Strict would hold the cookie back from that navigation
    private static CookieOptions CookieOptions(HttpContext context) =>
        new()
        {
            Path = PageUrls.SiteHandoff,
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            MaxAge = StartLifetime,
        };

    // encrypted, so the return address can't be swapped, and expiring with the cookie
    private ITimeLimitedDataProtector Protector() => dataProtection.CreateProtector(CookieName).ToTimeLimitedDataProtector();

    private static string RandomToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
