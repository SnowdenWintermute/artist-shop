using ArtistShop.Web.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// the operator's Sign everyone out, which ends the other tests' sign-ins too
[Collection(RunAloneCollection.Name)]
public sealed class SignEveryoneOutTests(TestApp app)
{
    [Fact]
    public async Task EndingAllSignInsSignsOutEveryAccountOnEveryHost()
    {
        var site = await app.MakeSiteAsync();
        var other = await app.MakeAccountAsync();
        var onPlatform = await app.SignedInClientAsync(TestApp.PlatformHost, site.OwnerEmail);
        var onSite = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var otherOnPlatform = await app.SignedInClientAsync(TestApp.PlatformHost, other);

        await app.Services.GetRequiredService<DatabaseTicketStore>().EndAllAsync();

        Assert.False(await SessionTests.IsSignedInAsync(onPlatform));
        Assert.False(await SessionTests.IsSignedInAsync(otherOnPlatform));
        var response = await onSite.GetAsync("/admin", TestContext.Current.CancellationToken);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }
}
