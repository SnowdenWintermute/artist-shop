using System.Security.Claims;
using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace ArtistShop.Web.Tests.App;

// who Google says someone is, as its handler reads it from Google's answer
public sealed record FakeGoogleLogin(string Email, string GoogleId, bool EmailVerified);

// Stands in for the trip to Google: an address, ahead of the app's own, that leaves the external
// cookie as Google's handler does on its way back, so a test can then follow the app's
// /Account/ExternalLogin as a browser would (TestApp.SignInWithGoogleAsync)
public sealed class FakeGoogle : IStartupFilter
{
    public const string Path = "/test/fake-google";

    // not Map, which would make Path the cookie's path
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use(
                async (context, nextMiddleware) =>
                {
                    if (context.Request.Path == Path)
                    {
                        await SignInAsync(context);
                        return;
                    }

                    await nextMiddleware(context);
                }
            );
            next(app);
        };

    private static async Task SignInAsync(HttpContext context)
    {
        var query = context.Request.Query;
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, query["googleId"].ToString()),
                new Claim(ClaimTypes.Email, query["email"].ToString()),
                new Claim(ExternalAccounts.EmailVerifiedClaimType, query["emailVerified"].ToString()),
            ],
            "Google"
        );
        // the item SignInManager.GetExternalLoginInfoAsync reads the provider from
        var properties = new AuthenticationProperties { Items = { ["LoginProvider"] = "Google" } };

        await context.SignInAsync(IdentityConstants.ExternalScheme, new ClaimsPrincipal(identity), properties);
    }
}
