namespace ArtistShop.Web.Database.Repositories;

using System.Text.Json;
using ArtistShop.Web.Domain.Website;
using Dapper;
using Npgsql;

public class ThemeRepository(SiteDatabase database)
{
    private const string UniqueNameConstraint = "unique_themes_name";

    public async Task<List<SavedTheme>> GetAllAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<ThemeRow>("SELECT * FROM get_all_themes()");

        return [.. rows.Select(row => new SavedTheme(new ThemeId(row.Id), row.Name, ReadSettings(row.Settings)))];
    }

    // Npgsql sends a C# string as text, and Postgres has no implicit cast from text to jsonb, so
    // the settings are cast in the call or the function isn't found
    public async Task<ThemeId> AddAsync(string name, Theme theme)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            var id = await connection.QuerySingleAsync<int>(
                "SELECT add_theme(@Name, CAST(@Settings AS jsonb))",
                new { Name = name, Settings = WriteSettings(theme) }
            );

            return new ThemeId(id);
        }
        catch (PostgresException exception) when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
        {
            throw new NameAlreadyInUseException(name);
        }
    }

    public async Task UpdateAsync(ThemeId id, string name, Theme theme)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT update_theme(@Id, @Name, CAST(@Settings AS jsonb))",
                new
                {
                    Id = id.Value,
                    Name = name,
                    Settings = WriteSettings(theme),
                }
            );
        }
        catch (PostgresException exception) when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
        {
            throw new NameAlreadyInUseException(name);
        }
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.ThemeNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    // a website using the theme goes back to Paper
    public async Task DeleteAsync(ThemeId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync("SELECT delete_theme(@Id, @Preset)", new { Id = id.Value, Preset = ThemePresets.Paper.Preset });
    }

    // what the public pages show. Null when it's a preset ThemePresets no longer has
    public async Task<NamedTheme?> GetInUseAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleAsync<InUseRow>("SELECT * FROM get_theme_in_use()");

        return row switch
        {
            { ThemeId: { } id, Name: { } name, Settings: { } settings } => new NamedTheme(
                new ThemeKey.Saved(new ThemeId(id)),
                name,
                ReadSettings(settings)
            ),
            { Preset: { } preset } => ThemePresets.Find(preset)?.Named,
            _ => throw new InvalidOperationException("theme_in_use holds neither a theme nor a preset."),
        };
    }

    public async Task UseAsync(ThemeKey key)
    {
        await using var connection = await database.OpenConnectionAsync();
        int? themeId = key is ThemeKey.Saved saved ? saved.Id.Value : null;
        ThemePreset? preset = key is ThemeKey.Preset chosen ? chosen.Value : null;

        try
        {
            await connection.ExecuteAsync("SELECT use_theme(@ThemeId, @Preset)", new { ThemeId = themeId, Preset = preset });
        }
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.ThemeNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    private static string WriteSettings(Theme theme) =>
        JsonSerializer.Serialize(
            new ThemeSettings
            {
                Colors = theme.Colors.Chosen.ToDictionary(entry => entry.Key.ToString(), entry => entry.Value.Hex),
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

    // a role no longer in ColorRole is dropped rather than refused, so a theme keeps showing what it
    // can after a role is retired
    private static Theme ReadSettings(string json)
    {
        var settings = JsonSerializer.Deserialize<ThemeSettings>(json, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("A theme's settings are null.");
        var colors = new Dictionary<ColorRole, RgbColor>();

        foreach (var (name, hex) in settings.Colors)
        {
            if (Enum.TryParse<ColorRole>(name, out var role) && Enum.IsDefined(role))
            {
                colors[role] = RgbColor.Parse(hex);
            }
        }

        return new Theme(new ThemeColors(colors), ThemeFonts.From(role => ReadFont(settings, role)));
    }

    // A font no longer in Font, or one a role can no longer have, shows Paper's, as does a theme saved
    // before themes had fonts
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

    // the settings column's document
    private sealed class ThemeSettings
    {
        public Dictionary<string, string> Colors { get; init; } = [];

        // by FontRole name
        public Dictionary<string, FontSettings> Fonts { get; init; } = [];
    }

    private sealed class FontSettings
    {
        public required string Font { get; init; }
        public required int SizePercent { get; init; }
    }

    private sealed class ThemeRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Settings { get; init; }
    }

    private sealed class InUseRow
    {
        public required ThemePreset? Preset { get; init; }
        public required int? ThemeId { get; init; }
        public required string? Name { get; init; }
        public required string? Settings { get; init; }
    }
}
