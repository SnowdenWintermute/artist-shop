namespace ArtistShop.Web.Security;

using System.Security.Cryptography;

// A per-request nonce for the one inline script, the import map. Scoped, so the header and the page
// of one request share it
public sealed class ContentSecurityNonce
{
    public string Value { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
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
                var nonce = context.RequestServices.GetRequiredService<ContentSecurityNonce>().Value;

                context.Response.Headers.ContentSecurityPolicy = string.Join(
                    "; ",
                    "default-src 'self'",
                    $"script-src 'self' 'nonce-{nonce}'",
                    "style-src 'self' 'unsafe-inline'",
                    "img-src 'self' data: blob:",
                    $"connect-src {connectSources}",
                    "frame-src 'self' https://www.youtube-nocookie.com https://player.vimeo.com",
                    "frame-ancestors 'self'",
                    "object-src 'none'",
                    "base-uri 'self'"
                );

                await next(context);
            }
        );
    }
}
