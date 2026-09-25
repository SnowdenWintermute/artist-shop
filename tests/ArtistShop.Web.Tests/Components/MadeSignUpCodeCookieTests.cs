using ArtistShop.Web.Components.Pages.Platform.Operator;
using ArtistShop.Web.Domain.Platform;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace ArtistShop.Web.Tests.Components;

public sealed class MadeSignUpCodeCookieTests
{
    // keys held in memory for the test's life
    private readonly IDataProtectionProvider _dataProtection = new EphemeralDataProtectionProvider();

    [Fact]
    public void ThePageAfterTheRedirectGetsTheCodeThatWasMade()
    {
        var code = SignUpCode.New();
        var cookie = KeptCookie(code);

        Assert.Equal(code.Text, MadeSignUpCodeCookie.Take(RequestWith(cookie), _dataProtection)?.Text);
    }

    // so a refresh of that page doesn't show it again
    [Fact]
    public void TakingTheCodeDeletesTheCookie()
    {
        var context = RequestWith(KeptCookie(SignUpCode.New()));

        MadeSignUpCodeCookie.Take(context, _dataProtection);

        var deletion = SetCookieOf(context);
        Assert.Equal("/operator", deletion.Path.Value);
        Assert.True(deletion.Expires < DateTimeOffset.UtcNow);
    }

    // it's encrypted, not the code as typed
    [Fact]
    public void TheCookieDoesntHoldTheCodeAsText()
    {
        var code = SignUpCode.New();

        Assert.DoesNotContain(code.Text, KeptCookie(code).Value.Value);
    }

    [Fact]
    public void AnAlteredCookieGivesNoCode()
    {
        var cookie = KeptCookie(SignUpCode.New());

        var altered = new CookieHeaderValue(cookie.Name, "x" + cookie.Value.Value);

        Assert.Null(MadeSignUpCodeCookie.Take(RequestWith(altered), _dataProtection));
    }

    [Fact]
    public void NoCookieGivesNoCode() =>
        Assert.Null(MadeSignUpCodeCookie.Take(new DefaultHttpContext(), _dataProtection));

    // the cookie the form's response sets, as the browser sends it back
    private CookieHeaderValue KeptCookie(SignUpCode code)
    {
        var context = new DefaultHttpContext();

        MadeSignUpCodeCookie.Keep(context, _dataProtection, code);

        var setCookie = SetCookieOf(context);
        return new CookieHeaderValue(setCookie.Name, setCookie.Value);
    }

    private static DefaultHttpContext RequestWith(CookieHeaderValue cookie)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = cookie.ToString();
        return context;
    }

    private static SetCookieHeaderValue SetCookieOf(HttpContext context) =>
        SetCookieHeaderValue.Parse(Assert.Single(context.Response.Headers.SetCookie.ToArray()));
}
