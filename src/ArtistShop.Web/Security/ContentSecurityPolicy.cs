namespace ArtistShop.Web.Security;

using System.Security.Cryptography;

// A per-request nonce for the one inline script, the import map. Kept on the HttpContext rather than
// in a scoped service: the not-found and error pages are rendered by re-running the request in a
// new scope, and the page they render must carry the header's nonce
public static class ContentSecurityNonce
{
    private static readonly object ItemKey = new();

    public static string Of(HttpContext context)
    {
        if (context.Items.TryGetValue(ItemKey, out var existing) && existing is string nonce)
        {
            return nonce;
        }

        var created = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        context.Items[ItemKey] = created;
        return created;
    }
}

// Scripts only from this host, or inline with the request's nonce, so an encoding bug on a site's
// page can't run script that sets cookies for the platform (see "same-site" in todo.md). Styles
// allow inline, as Blazor and Quill write style attributes. Frames: this host (the artwork picker)
// and the two video players; only this host may frame its pages
public static class ContentSecurityPolicy
{
    public static void UseContentSecurityPolicy(this WebApplication app)
    {
        // dotnet watch's browser refresh talks to its own port over a websocket, named here
        var connectSources = app.Environment.IsDevelopment()
            && Environment.GetEnvironmentVariable("ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT") is { Length: > 0 } refresh
                ? $"'self' {refresh.Replace(',', ' ')}"
                : "'self'";

        app.Use(
            async (context, next) =>
            {
                // as the response starts, since the exception handler clears every header set before
                // it re-runs the request for the error page. Replaces the frame-ancestors-only policy
                // Blazor adds, which this one includes
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.ContentSecurityPolicy = string.Join(
                        "; ",
                        "default-src 'self'",
                        $"script-src 'self' 'nonce-{ContentSecurityNonce.Of(context)}'",
                        "style-src 'self' 'unsafe-inline'",
                        "img-src 'self' data: blob:",
                        $"connect-src {connectSources}",
                        "frame-src 'self' https://www.youtube-nocookie.com https://player.vimeo.com",
                        "frame-ancestors 'self'",
                        "object-src 'none'",
                        "base-uri 'self'"
                    );

                    return Task.CompletedTask;
                });

                await next(context);
            }
        );
    }
}
