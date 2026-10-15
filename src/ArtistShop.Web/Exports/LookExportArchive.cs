using System.IO.Compression;
using System.Text.Json.Nodes;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Images;

namespace ArtistShop.Web.Exports;

// The look and wording download: look.json, and each brand image's original in branding/
public static class LookExportArchive
{
    // the folder's name inside Download everything
    public const string WebsiteFolder = "look-and-wording";

    public const string BrandingFolder = "branding";

    public const string FileName = "look.json";
    public const int FormatVersion = 1;
    public const string FormatVersionProperty = "formatVersion";

    public const string WordingProperty = "wording";
    public const string CollectionWordingProperty = "collection";
    public const string WorkWordingProperty = "work";
    public const string DescriptionWordingProperty = "description";
    public const string LabelProperty = "label";
    public const string SingularProperty = "singular";
    public const string PluralProperty = "plural";
    public const string KeepsCaseProperty = "keepsCase";

    // a preset by its name, or a saved theme by its name in themes
    public const string ThemeInUseProperty = "themeInUse";
    public const string PresetProperty = "preset";
    public const string SavedThemeProperty = "theme";

    public const string ThemesProperty = "themes";
    public const string NameProperty = "name";
    public const string SettingsProperty = "settings";

    public const string BrandImagesProperty = "brandImages";
    public const string TypeProperty = "type";
    public const string FileProperty = "file";
    public const string Sha256Property = "sha256";

    public static async Task WriteAsync(
        Stream destination,
        SiteLook look,
        string folderName,
        BrandImageStore brandImageStore,
        CancellationToken cancellationToken
    )
    {
        await using var zip = await ExportZip.CreateAsync(destination, cancellationToken);

        await ExportZip.AddTextAsync(zip, $"{folderName}/{ExportZip.ReadmeFileName}", Readme, cancellationToken);
        await AddAsync(zip, look, folderName, brandImageStore, cancellationToken);
    }

    // the same files, into a zip being written, as the whole-website download does
    public static async Task AddAsync(
        ZipArchive zip,
        SiteLook look,
        string folderName,
        BrandImageStore brandImageStore,
        CancellationToken cancellationToken
    )
    {
        var brandImages = new JsonArray();

        foreach (var image in look.BrandImages)
        {
            await using var original = brandImageStore.OpenOriginal(image);

            // left out, as a work image's missing original is
            if (original is null)
            {
                continue;
            }

            // an SVG has no format here, so its uploaded name's extension is used, as for a work image
            var format = await ExportImageFormat.ReadAsync(original, cancellationToken);
            var extension = format?.Extension ?? Path.GetExtension(image.OriginalFileName).ToLowerInvariant();
            var file = $"{BrandingFolder}/{BrandFileName(image.Type)}{extension}";
            var sha256 = await ExportZip.AddFileWithSha256Async(
                zip,
                $"{folderName}/{file}",
                original,
                ExportZip.CompressionFor(format),
                cancellationToken
            );

            brandImages.Add(
                new JsonObject
                {
                    [TypeProperty] = image.Type.ToString(),
                    [FileProperty] = file,
                    [Sha256Property] = sha256,
                }
            );
        }

        // last, since each image's hash is only known once it's been copied
        await ExportZip.AddTextAsync(zip, $"{folderName}/{FileName}", Json(look, brandImages), cancellationToken);
    }

    private static string Json(SiteLook look, JsonArray brandImages)
    {
        var themes = new JsonArray();

        foreach (var theme in look.Themes)
        {
            themes.Add(
                new JsonObject
                {
                    [NameProperty] = theme.Name,
                    [SettingsProperty] = JsonNode.Parse(ThemeSettingsJson.Write(theme.Theme)),
                }
            );
        }

        var file = new JsonObject
        {
            [FormatVersionProperty] = FormatVersion,
            [WordingProperty] = new JsonObject
            {
                [CollectionWordingProperty] = NounObject(look.Wording.CollectionChoice),
                [WorkWordingProperty] = NounObject(look.Wording.WorkChoice),
                [DescriptionWordingProperty] = LabelObject(look.Wording.DescriptionChoice),
            },
            [ThemeInUseProperty] = ThemeInUseObject(look),
            [ThemesProperty] = themes,
            [BrandImagesProperty] = brandImages,
        };

        return file.ToJsonString(PostExportJson.Readable);
    }

    private static JsonObject ThemeInUseObject(SiteLook look) =>
        look.ThemeInUse.Key switch
        {
            ThemeKey.Preset preset => new JsonObject { [PresetProperty] = preset.Value.ToString() },
            ThemeKey.Saved => new JsonObject { [SavedThemeProperty] = look.ThemeInUse.Name },
            _ => throw new InvalidOperationException($"{look.ThemeInUse.Key.GetType().Name} isn't a theme key."),
        };

    // null words stay null, so a default carries over as the default rather than as today's words
    private static JsonObject NounObject(NounChoice choice) =>
        new()
        {
            [SingularProperty] = choice.Singular,
            [PluralProperty] = choice.Plural,
            [KeepsCaseProperty] = choice.KeepsCase,
        };

    private static JsonObject LabelObject(LabelChoice choice) =>
        new() { [LabelProperty] = choice.Text, [KeepsCaseProperty] = choice.KeepsCase };

    private static string BrandFileName(BrandImageType type) =>
        type switch
        {
            BrandImageType.Logo => "logo",
            BrandImageType.LogoOnDark => "logo-on-dark",
            BrandImageType.Favicon => "favicon",
            _ => throw new InvalidOperationException($"{type} isn't a brand image type."),
        };

    private const string Readme = $"""
        Look and wording

        {FileName}
          The website's wording, its theme in use and its saved themes. Don't change it by hand.

        {BrandingFolder}/
          The logos and browser tab icon, as they were uploaded.

        Importing into another website here
          Choose this whole folder, unzipped, under Import > Look and wording.
        """;
}
