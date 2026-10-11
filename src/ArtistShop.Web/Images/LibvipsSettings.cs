namespace ArtistShop.Web.Images;

// libvips' settings are global to the process, so the app sets them once at startup, and tests that
// process images set the same ones
public static class LibvipsSettings
{
    public static void Apply()
    {
        // refuse libvips file readers that aren't built to handle hostile files.
        // full name needed: "NetVips" alone means the namespace, and the setting lives on the NetVips class inside it
        NetVips.NetVips.BlockUntrusted = true;

        // except SVG, for logos and favicons. libvips marks it untrusted as it hasn't fuzzed librsvg itself;
        // librsvg is written in Rust, and Wikipedia renders its users' SVG uploads with it
        NetVips.Operation.Block("VipsForeignLoadSvg", false);

        // one thread per image; ImageProcessingLimiter decides how many images run at once
        NetVips.NetVips.Concurrency = 1;

        // libvips caches recent operations to reuse them, but each upload is processed once, so the cache
        // would only hold memory the limiter's estimates don't count
        NetVips.Cache.Max = 0;
    }
}
