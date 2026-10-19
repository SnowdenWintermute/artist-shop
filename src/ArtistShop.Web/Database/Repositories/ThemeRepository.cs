namespace ArtistShop.Web.Database.Repositories;

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
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    // a website using the theme goes back to Paper
    public async Task DeleteAsync(ThemeId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync("SELECT delete_theme(@Id, @Preset)", new { Id = id.Value, Preset = ThemePresets.Paper.Preset });
    }

    // what the public pages show: the theme in use, or Paper for a preset ThemePresets no longer has
    public async Task<NamedTheme> GetShownAsync() => await GetInUseAsync() ?? ThemePresets.Paper.Named;

    // the theme in use. Null when it's a preset ThemePresets no longer has
    public async Task<NamedTheme?> GetInUseAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleAsync<InUseRow>("SELECT * FROM get_theme_in_use()");

        return row is { ThemeId: null, Preset: null }
            ? throw new InvalidOperationException("theme_in_use holds neither a theme nor a preset.")
            : NamedThemeOf(row.ThemeId, row.Name, row.Settings, row.Preset);
    }

    // a saved theme's columns, or a ThemePreset's id, as a query that joins themes reads them. Null for
    // neither, or a preset since retired
    public static NamedTheme? NamedThemeOf(int? themeId, string? name, string? settings, ThemePreset? preset) =>
        (themeId, name, settings, preset) switch
        {
            ({ } id, { } savedName, { } json, _) => new NamedTheme(new ThemeKey.Saved(new ThemeId(id)), savedName, ReadSettings(json)),
            (_, _, _, { } chosen) => ThemePresets.Find(chosen)?.Named,
            _ => null,
        };

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
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    private static string WriteSettings(Theme theme) => ThemeSettingsJson.Write(theme);

    private static Theme ReadSettings(string json) =>
        ThemeSettingsJson.TryRead(json, out var theme)
            ? theme
            : throw new InvalidOperationException("A theme's settings aren't readable.");

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
