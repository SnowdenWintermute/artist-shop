using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Website;
using static ArtistShop.Web.Exports.LookExportArchive;
using static ArtistShop.Web.Imports.ImportJson;

namespace ArtistShop.Web.Imports;

// the theme a download was using: a preset, or one of its saved themes by name
public abstract record ThemeInUseChoice
{
    public sealed record Preset(ThemePreset Value) : ThemeInUseChoice;

    public sealed record Saved(string Name) : ThemeInUseChoice;

    // a preset this website doesn't have, by the name the download gave it
    public sealed record UnknownPreset(string Name) : ThemeInUseChoice;
}

public record LookImportTheme(string Name, Theme Theme);

// File is its path beside look.json
public record LookImportBrandImage(BrandImageType Type, string File, string Sha256);

// look.json from the look and wording download, checked against what the website's own pages could
// have saved, so nothing read from it can fail to save. A preset or brand image type this website
// doesn't have is left out with a note rather than refusing the file; UnknownBrandImageTypes are
// those types' names
public record LookImportFile(
    SiteWording Wording,
    ThemeInUseChoice ThemeInUse,
    IReadOnlyList<LookImportTheme> Themes,
    IReadOnlyList<LookImportBrandImage> BrandImages,
    IReadOnlyList<string> UnknownBrandImageTypes
)
{
    public static bool TryRead(string? json, [NotNullWhen(true)] out LookImportFile? file, out string problem)
    {
        file = null;
        problem = $"The folder has no {FileName}. Choose the unzipped look and wording folder.";

        if (json is null)
        {
            return false;
        }

        JsonNode? root;

        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            root = null;
        }

        problem = $"Its {FileName} isn't readable.";

        if (root is not JsonObject look)
        {
            return false;
        }

        if (IntOf(look[FormatVersionProperty]) is not { } version || version != FormatVersion)
        {
            problem = $"Its {FileName} is from a version of the download this website doesn't read.";
            return false;
        }

        if (
            look[WordingProperty] is not JsonObject wording
            || NounOf(wording[CollectionWordingProperty]) is not { } collectionChoice
            || NounOf(wording[WorkWordingProperty]) is not { } workChoice
            || LabelOf(wording[DescriptionWordingProperty]) is not { } descriptionChoice
            || ThemesOf(look[ThemesProperty]) is not { } themes
            || ThemeInUseOf(look[ThemeInUseProperty], themes) is not { } themeInUse
            || BrandImagesOf(look[BrandImagesProperty], out var unknownBrandImageTypes) is not { } brandImages
        )
        {
            return false;
        }

        file = new LookImportFile(
            new SiteWording(collectionChoice, workChoice, descriptionChoice),
            themeInUse,
            themes,
            brandImages,
            unknownBrandImageTypes
        );
        return true;
    }

    // null for a name the Theme page couldn't have saved, or two of one name, which the database
    // takes as the same whatever their case
    private static List<LookImportTheme>? ThemesOf(JsonNode? node)
    {
        if (node is not JsonArray entries)
        {
            return null;
        }

        var themes = new List<LookImportTheme>();

        foreach (var entry in entries)
        {
            if (
                entry is not JsonObject theme
                || StringOf(theme[NameProperty])?.Trim() is not { Length: > 0 and <= ArtistShopLimits.ThemeNameMaximumLength } name
                || theme[SettingsProperty] is not JsonObject settings
                || !ThemeSettingsJson.TryRead(settings.ToJsonString(), out var read)
                || themes.Any(other => ImportNames.Comparer.Equals(other.Name, name))
            )
            {
                return null;
            }

            themes.Add(new LookImportTheme(name, read));
        }

        return themes;
    }

    // a saved theme has to be one of themes
    private static ThemeInUseChoice? ThemeInUseOf(JsonNode? node, List<LookImportTheme> themes)
    {
        if (node is not JsonObject inUse)
        {
            return null;
        }

        if (StringOf(inUse[PresetProperty]) is { } presetName)
        {
            return Enum.TryParse<ThemePreset>(presetName, out var preset)
                && preset.ToString() == presetName
                && ThemePresets.Find(preset) is not null
                ? new ThemeInUseChoice.Preset(preset)
                : new ThemeInUseChoice.UnknownPreset(presetName);
        }

        return StringOf(inUse[SavedThemeProperty]) is { } savedName
            && themes.FirstOrDefault(theme => ImportNames.Comparer.Equals(theme.Name, savedName.Trim())) is { } saved
            ? new ThemeInUseChoice.Saved(saved.Name)
            : null;
    }

    // one of each type at most
    private static List<LookImportBrandImage>? BrandImagesOf(JsonNode? node, out List<string> unknownTypes)
    {
        unknownTypes = [];

        if (node is not JsonArray entries)
        {
            return null;
        }

        var images = new List<LookImportBrandImage>();

        foreach (var entry in entries)
        {
            if (
                entry is not JsonObject image
                || StringOf(image[TypeProperty]) is not { } typeName
                || StringOf(image[FileProperty]) is not { Length: > 0 } path
                || StringOf(image[Sha256Property]) is not { Length: 64 } sha256
                || unknownTypes.Contains(typeName)
            )
            {
                return null;
            }

            if (!Enum.TryParse<BrandImageType>(typeName, out var type) || !Enum.IsDefined(type) || type.ToString() != typeName)
            {
                unknownTypes.Add(typeName);
                continue;
            }

            if (images.Any(other => other.Type == type))
            {
                return null;
            }

            images.Add(new LookImportBrandImage(type, path, sha256.ToLowerInvariant()));
        }

        return images;
    }

    // null for anything the Wording page couldn't have saved
    private static NounChoice? NounOf(JsonNode? node)
    {
        if (
            node is not JsonObject noun
            || !TryWordOf(noun[SingularProperty], out var singular)
            || !TryWordOf(noun[PluralProperty], out var plural)
            || (singular is null) != (plural is null)
            || BoolOf(noun[KeepsCaseProperty]) is not { } keepsCase
        )
        {
            return null;
        }

        return new NounChoice(singular, plural, keepsCase);
    }

    // null for anything the Wording page couldn't have saved
    private static LabelChoice? LabelOf(JsonNode? node)
    {
        if (
            node is not JsonObject label
            || !TryWordOf(label[LabelProperty], out var text)
            || BoolOf(label[KeepsCaseProperty]) is not { } keepsCase
        )
        {
            return null;
        }

        return new LabelChoice(text, keepsCase);
    }

    // a missing word is null, the default; one that's there has to be a word the database takes
    private static bool TryWordOf(JsonNode? node, out string? word)
    {
        word = NounChoice.Word(StringOf(node));
        return node is null || word is { Length: <= ArtistShopLimits.WordingWordMaximumLength };
    }
}
