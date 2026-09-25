namespace ArtistShop.Web.Sites;

using Microsoft.AspNetCore.Components.Endpoints;

[Flags]
public enum HostTypes : byte
{
    // the platform's own host, where artists sign up
    Platform = 1,

    // any site's host
    Site = 2,
}

// Which types of host serve a page or endpoint; UseServedOnHosts answers any other with a 404. Pages
// without it are a site's. Other endpoints without it (static files, Blazor's connection, logout)
// serve every host
[AttributeUsage(AttributeTargets.Class)]
public sealed class ServedOnAttribute(HostTypes hosts) : Attribute
{
    public HostTypes Hosts { get; } = hosts;

    // ASP.NET puts a page's attributes in its endpoint's metadata, as it does [Authorize]
    public static HostTypes For(EndpointMetadataCollection metadata) =>
        metadata.GetMetadata<ServedOnAttribute>()?.Hosts
        ?? (metadata.GetMetadata<ComponentTypeMetadata>() is null ? HostTypes.Platform | HostTypes.Site : HostTypes.Site);
}
