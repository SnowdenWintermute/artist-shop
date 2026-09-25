namespace ArtistShop.Web.Sites;

public static class HostRequests
{
    // A host that's neither the platform's nor a site's gets a plain 404 before anything else runs,
    // so nothing after this has to ask which host a request is for and find none
    public static void UseKnownHosts(this WebApplication app)
    {
        var directory = app.Services.GetRequiredService<HostDirectory>();

        app.Use(
            async (context, next) =>
            {
                if (directory.Find(context.Request.Host.Host) is null)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                await next(context);
            }
        );
    }

    // A page or endpoint the host doesn't serve (ServedOnAttribute) is a 404. After the status code
    // pages, so it gets the not-found page, which every host serves
    public static void UseServedOnHosts(this WebApplication app) =>
        app.Use(
            async (context, next) =>
            {
                var hostType = context.RequestServices.GetRequiredService<CurrentHost>().Type;

                if (context.GetEndpoint() is { } endpoint && (ServedOnAttribute.For(endpoint.Metadata) & hostType) == 0)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                await next(context);
            }
        );
}
