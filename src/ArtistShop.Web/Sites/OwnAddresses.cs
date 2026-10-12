namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Domain.Website;
using Microsoft.AspNetCore.Routing.Patterns;

// The one-word addresses our own pages and endpoints answer, such as /admin and /posts, read from the
// routes so a new page is covered without a list to keep. A custom page can't have one: the literal
// route wins over /{address}, so the page would never show
public class OwnAddresses(EndpointDataSource endpoints)
{
    public bool Contains(CustomPageAddress address) =>
        endpoints
            .Endpoints.OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.PathSegments)
            .Any(segments =>
                segments is [{ Parts: [RoutePatternLiteralPart literal] }]
                && string.Equals(literal.Content, address.Value, StringComparison.OrdinalIgnoreCase)
            );
}
