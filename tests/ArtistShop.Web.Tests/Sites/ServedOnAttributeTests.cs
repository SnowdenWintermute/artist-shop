using ArtistShop.Web.Components.Pages;
using ArtistShop.Web.Sites;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Http;

namespace ArtistShop.Web.Tests.Sites;

public sealed class ServedOnAttributeTests
{
    [Fact]
    public void APageIsASitesUnlessMarked()
    {
        var metadata = new EndpointMetadataCollection(new ComponentTypeMetadata(typeof(SiteHome)));

        Assert.Equal(HostTypes.Site, ServedOnAttribute.For(metadata));
    }

    [Fact]
    public void AMarkedPageIsServedWhereItsMarkSays()
    {
        var metadata = new EndpointMetadataCollection(
            new ComponentTypeMetadata(typeof(Home)),
            new ServedOnAttribute(HostTypes.Platform)
        );

        Assert.Equal(HostTypes.Platform, ServedOnAttribute.For(metadata));
    }

    // static files, Blazor's connection and logout
    [Fact]
    public void AnEndpointThatIsntAPageServesEveryHostUnlessMarked()
    {
        Assert.Equal(HostTypes.Platform | HostTypes.Site, ServedOnAttribute.For(new EndpointMetadataCollection()));
        Assert.Equal(
            HostTypes.Site,
            ServedOnAttribute.For(new EndpointMetadataCollection(new ServedOnAttribute(HostTypes.Site)))
        );
    }

    // what ASP.NET reads for the real pages: their attributes as the endpoint's metadata
    [Fact]
    public void HomeAndLoginServeEveryHost()
    {
        Assert.Equal(HostTypes.Platform | HostTypes.Site, ServedOnOf(typeof(Home)));
        Assert.Equal(HostTypes.Platform | HostTypes.Site, ServedOnOf(typeof(Web.Components.Account.EveryHost.Login)));
    }

    // signing in and the account's own pages are the platform's; a website only receives the handoff
    [Fact]
    public void TheOtherAccountPagesServeOneKindOfHost()
    {
        Assert.Equal(HostTypes.Platform, ServedOnOf(typeof(Web.Components.Account.Pages.ForgotPassword)));
        Assert.Equal(HostTypes.Platform, ServedOnOf(typeof(Web.Components.Account.Pages.Manage.Index)));
        Assert.Equal(HostTypes.Site, ServedOnOf(typeof(Web.Components.Account.OnWebsites.Handoff)));
    }

    private static HostTypes ServedOnOf(Type page) =>
        ServedOnAttribute.For(
            new EndpointMetadataCollection([new ComponentTypeMetadata(page), .. page.GetCustomAttributes(inherit: true)])
        );
}
