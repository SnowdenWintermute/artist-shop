namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Domain.Sites;
using Microsoft.AspNetCore.Components.Server.Circuits;

// The site a request or a circuit is for, as a scoped service, found from its host
public sealed record CurrentSite(SiteId Id)
{
    // A circuit is made while its connection's request is at hand, so it reads the host the same
    // way. SiteCircuitStart makes sure that's when a circuit's CurrentSite is made
    public static CurrentSite From(IServiceProvider services)
    {
        var request =
            services.GetRequiredService<IHttpContextAccessor>().HttpContext?.Request
            ?? throw new InvalidOperationException("The current site was asked for with no request, so no host to find it by.");

        var siteId =
            services.GetRequiredService<SiteHostDirectory>().FindSite(request.Host.Host)
            ?? throw new InvalidOperationException(
                $"No site has the host \"{request.Host.Host}\", which UseSiteHosts should have answered with a 404."
            );

        return new CurrentSite(siteId);
    }
}

// Blazor makes every circuit handler as it makes the circuit, while the connection's request is
// still there to read. Taking CurrentSite here makes the circuit's copy then. Left to be made on
// first use, it could be during an event, when Blazor has no request to hand
public sealed class SiteCircuitStart(CurrentSite currentSite) : CircuitHandler
{
    public CurrentSite Site { get; } = currentSite;
}

public static class SiteHostRequests
{
    // a host no site has gets a plain 404 before anything else runs, so nothing after this ever
    // has to ask which site a request is for and find none
    public static void UseSiteHosts(this WebApplication app)
    {
        var directory = app.Services.GetRequiredService<SiteHostDirectory>();

        app.Use(
            async (context, next) =>
            {
                if (directory.FindSite(context.Request.Host.Host) is null)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                await next(context);
            }
        );
    }
}
