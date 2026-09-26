namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Domain.Sites;
using Microsoft.AspNetCore.Components.Server.Circuits;

// Which of the app's hosts a request or a circuit is for, as a scoped service: the platform's own,
// or one of the sites'
public abstract record CurrentHost
{
    // only the two below
    private CurrentHost() { }

    public sealed record Platform : CurrentHost;

    // MainHost is the site's own, whichever of its hosts the request came in on
    public sealed record Site(SiteId Id, HostName MainHost) : CurrentHost;

    public HostTypes Type =>
        this switch
        {
            Platform => HostTypes.Platform,
            Site => HostTypes.Site,
            _ => throw new InvalidOperationException($"{GetType().Name} isn't a host type."),
        };

    // A circuit is made while its connection's request is at hand, so it reads the host the same
    // way. HostCircuitStart makes sure that's when a circuit's CurrentHost is made
    public static CurrentHost From(IServiceProvider services)
    {
        var request =
            services.GetRequiredService<IHttpContextAccessor>().HttpContext?.Request
            ?? throw new InvalidOperationException("The current host was asked for with no request to read it from.");

        return services.GetRequiredService<HostDirectory>().Find(request.Host.Host)
            ?? throw new InvalidOperationException(
                $"\"{request.Host.Host}\" is neither the platform's host nor a site's, which UseKnownHosts should have answered with a 404."
            );
    }
}

// Blazor makes every circuit handler as it makes the circuit, while the connection's request is
// still there to read. Taking CurrentHost here makes the circuit's copy then. Left to be made on
// first use, it could be during an event, when Blazor has no request to hand
public sealed class HostCircuitStart(CurrentHost currentHost) : CircuitHandler
{
    public CurrentHost Host { get; } = currentHost;
}
