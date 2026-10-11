namespace ArtistShop.Web.Domain.Website;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

// A theme as one JSON document, as the themes table's settings column and look.json both keep it:
// only what the theme chose, keyed by C#'s enum names, as {"colors": {"Page": "#202020"}, "fonts": {...}}
public static class ThemeSettingsJson
{
    // in ColorRole's order, so two themes choosing the same colours write the same text
    public static string Write(Theme theme) =>
        JsonSerializer.Serialize(
            new ThemeSettings
            {
                Colors = theme
                    .Colors.Chosen.OrderBy(entry => entry.Key)
                    .ToDictionary(entry => entry.Key.ToString(), entry => entry.Value.Hex),
                Fonts = FontRoles.All.ToDictionary(
                    definition => definition.Role.ToString(),
                    definition => new FontSettings
                    {
                        Font = theme.Fonts.For(definition.Role).Font.ToString(),
                        SizePercent = theme.Fonts.For(definition.Role).SizePercent,
                    }
                ),
            },
            JsonSerializerOptions.Web
        );

    // False for a document Write couldn't have written, or one missing a base role. A role no
    // longer in ColorRole is dropped rather than refused, so a theme keeps showing what it can
    // after a role is retired
    public static bool TryRead(string json, [NotNullWhen(true)] out Theme? theme)
    {
        theme = null;
        ThemeSettings? settings;

        try
        {
            settings = JsonSerializer.Deserialize<ThemeSettings>(json, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return false;
        }

        if (settings is null)
        {
            return false;
        }

        var colors = new Dictionary<ColorRole, RgbColor>();

        foreach (var (name, hex) in settings.Colors)
        {
            if (!RgbColor.TryParse(hex, out var color))
            {
                return false;
            }

            if (Enum.TryParse<ColorRole>(name, out var role) && Enum.IsDefined(role) && role.ToString() == name)
            {
                colors[role] = color;
            }
        }

        // ThemeColors needs every base role
        if (ColorRoles.All.Any(definition => definition.IsBase && !colors.ContainsKey(definition.Role)))
        {
            return false;
        }

        theme = new Theme(new ThemeColors(colors), ThemeFonts.From(role => ReadFont(settings, role)));
        return true;
    }

    // A font no longer in Font, or one a role can no longer have, shows Paper's
    private static FontChoice ReadFont(ThemeSettings settings, FontRole role)
    {
        if (
            settings.Fonts.GetValueOrDefault(role.ToString()) is { } stored
            && Enum.TryParse<Font>(stored.Font, out var font)
            && font.ToString() == stored.Font
            && FontRoles.For(role).Allows(font)
            && FontRoles.For(role).AllowsSize(stored.SizePercent)
        )
        {
            return new FontChoice(font, stored.SizePercent);
        }

        return ThemePresets.Paper.Theme.Fonts.For(role);
    }

    private sealed class ThemeSettings
    {
        public Dictionary<string, string> Colors { get; init; } = [];

        // by FontRole name
        public required Dictionary<string, FontSettings> Fonts { get; init; }
    }

    private sealed class FontSettings
    {
        public required string Font { get; init; }
        public required int SizePercent { get; init; }
    }
}
