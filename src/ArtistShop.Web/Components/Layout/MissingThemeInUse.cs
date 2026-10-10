namespace ArtistShop.Web.Components.Layout;

// The website uses a preset ThemePresets no longer has. Development throws, so a retired preset is
// noticed before it ships; production logs it and the public pages keep the platform's colours,
// which App.razor sets on :root
public static class MissingThemeInUse
{
    private const string Message = "The website's theme in use is a preset that no longer exists.";

    public static void Report(ILogger logger, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            throw new InvalidOperationException(Message);
        }

        logger.LogError(Message);
    }
}
