using System.ComponentModel.DataAnnotations;

namespace ArtistShop.Web.Utilities;

public static class ValidatedSettings
{
    // Get<T> fills T's properties from the section's keys by name. A key that's missing leaves the
    // property at its default (0, or a zero TimeSpan), which the [Range] attributes then reject
    public static T Read<T>(IConfiguration configuration, string sectionName)
        where T : class
    {
        var settings =
            configuration
                .GetRequiredSection(sectionName)
                // a misspelled key in appsettings.json is an error rather than silently ignored
                .Get<T>(options => options.ErrorOnUnknownConfiguration = true)
            ?? throw new InvalidOperationException($"{sectionName} has no settings.");

        try
        {
            Validator.ValidateObject(settings, new ValidationContext(settings), validateAllProperties: true);
        }
        catch (ValidationException exception)
        {
            throw new InvalidOperationException($"{sectionName}: {exception.Message}", exception);
        }

        return settings;
    }
}
