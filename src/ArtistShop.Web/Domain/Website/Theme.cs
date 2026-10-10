namespace ArtistShop.Web.Domain.Website;

using System.Globalization;

// How a website's public pages look. Fonts and sizes join the colours here
public sealed record Theme(ThemeColors Colors);

public record ThemeId(int Value);

// a theme the website saved from the Theme page, a copy that no preset's later changes reach
public record SavedTheme(ThemeId Id, string Name, Theme Theme)
{
    public NamedTheme Named => new(new ThemeKey.Saved(Id), Name, Theme);
}

// a preset or a saved theme, as the Theme page lists it
public record NamedTheme(ThemeKey Key, string Name, Theme Theme)
{
    public bool IsSaved => Key is ThemeKey.Saved;
}

// the ids are stored in theme_in_use.preset
public enum ThemePreset
{
    Paper = 1,
    Dark = 2,
}

public record ThemePresetDefinition(ThemePreset Preset, string Name, Theme Theme)
{
    public NamedTheme Named => new(new ThemeKey.Preset(Preset), Name, Theme);
}

// The themes every website can start from. Kept here rather than in a website's database, so a
// change to one reaches every website using it with the next deploy
public static class ThemePresets
{
    // the platform's own look, which admin pages always have and a new website starts with
    public static readonly ThemePresetDefinition Paper = new(
        ThemePreset.Paper,
        "Paper",
        Choosing(
            (ColorRole.Page, "#f5f5f5"),
            (ColorRole.Ink, "#000000"),
            (ColorRole.Accent, "#155dfc"),
            (ColorRole.Bar, "#e9e7e2"),
            (ColorRole.Placeholder, "#f3f4f6"),
            (ColorRole.InkFaded, "#5f6878"),
            (ColorRole.InkUnavailable, "#7d8594"),
            (ColorRole.LinkHover, "#0000ff")
        )
    );

    public static readonly IReadOnlyList<ThemePresetDefinition> All =
    [
        Paper,
        new(
            ThemePreset.Dark,
            "Dark",
            Choosing((ColorRole.Page, "#1c1d21"), (ColorRole.Ink, "#ebe8e2"), (ColorRole.Accent, "#99c1f1"), (ColorRole.Rule, "#5a5d66"))
        ),
    ];

    public static ThemePresetDefinition For(ThemePreset preset) =>
        Find(preset) ?? throw new InvalidOperationException($"{preset} isn't a preset.");

    // null for an id stored before its preset was retired
    public static ThemePresetDefinition? Find(ThemePreset preset) => All.SingleOrDefault(definition => definition.Preset == preset);

    private static Theme Choosing(params (ColorRole Role, string Hex)[] choices) =>
        new(new ThemeColors(choices.ToDictionary(choice => choice.Role, choice => RgbColor.Parse(choice.Hex))));
}

// Which theme: a preset or one the website saved. The website's theme in use is one, and so is the
// theme open on the Theme page, whose ?theme= is QueryValue
public abstract record ThemeKey
{
    public sealed record Preset(ThemePreset Value) : ThemeKey;

    public sealed record Saved(ThemeId Id) : ThemeKey;

    // a preset by name and a saved theme by number, so the two can't be mistaken for each other
    public string QueryValue =>
        this switch
        {
            Preset preset => preset.Value.ToString(),
            Saved saved => saved.Id.Value.ToString(CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"{GetType().Name} isn't a theme key."),
        };

    public static ThemeKey? FromQueryValue(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? new Saved(new ThemeId(id))
        : Enum.TryParse<ThemePreset>(value, ignoreCase: true, out var preset) && Enum.IsDefined(preset) ? new Preset(preset)
        : null;
}
