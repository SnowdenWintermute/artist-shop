namespace ArtistShop.Web.Components.Pages.Platform.Operator;

// An example site as ExampleSiteOrderList takes it: plain values, since an island's parameters
// cross as JSON, which can't make a HostName (it has no public constructor)
public record ExampleSiteItem(int SiteId, string Host, bool IsBeingDeleted);
