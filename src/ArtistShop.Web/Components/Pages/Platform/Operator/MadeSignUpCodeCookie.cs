using System.Security.Cryptography;
using ArtistShop.Web.Domain.Platform;
using Microsoft.AspNetCore.DataProtection;

namespace ArtistShop.Web.Components.Pages.Platform.Operator;

// Carries a code just made across the redirect after its form post, so the page the redirect loads
// shows it once and a browser refresh doesn't post the form again. Encrypted with Data Protection,
// since it holds the code itself, and read once: taking it deletes it
public static class MadeSignUpCodeCookie
{
    private const string Name = "ArtistShop.MadeSignUpCode";

    public static void Keep(HttpContext context, IDataProtectionProvider dataProtection, SignUpCode code) =>
        context.Response.Cookies.Append(Name, Protector(dataProtection).Protect(code.Text), Options(context));

    // null when there's none, or when it can't be decrypted: altered, or made with a key since retired
    public static SignUpCode? Take(HttpContext context, IDataProtectionProvider dataProtection)
    {
        if (!context.Request.Cookies.TryGetValue(Name, out var value))
        {
            return null;
        }

        context.Response.Cookies.Delete(Name, Options(context));

        try
        {
            return SignUpCode.Read(Protector(dataProtection).Unprotect(value));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    // the purpose keeps these apart from anything else the app encrypts
    private static IDataProtector Protector(IDataProtectionProvider dataProtection) =>
        dataProtection.CreateProtector(Name);

    // sent only to the operator page, and only long enough to follow the redirect
    private static CookieOptions Options(HttpContext context) =>
        new()
        {
            Path = PageUrls.Operator,
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            MaxAge = TimeSpan.FromMinutes(1),
        };
}
