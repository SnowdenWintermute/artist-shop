using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArtistShop.Web.Exports;

namespace ArtistShop.Web.Imports;

// website.json from Download everything: the list separator, and each artwork file's type
public record WebsiteImportManifest(char ListSeparator, IReadOnlyList<(string FileName, string ArtworkType)> ArtworkFiles)
{
    public static bool TryRead(string? json, [NotNullWhen(true)] out WebsiteImportManifest? manifest, out string problem)
    {
        manifest = null;
        problem = $"The folder has no {WebsiteExportArchive.ManifestFileName}. Choose the unzipped folder from Download everything.";

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

        problem = $"Its {WebsiteExportArchive.ManifestFileName} isn't readable.";

        if (root is not JsonObject file)
        {
            return false;
        }

        if (IntOf(file[WebsiteExportArchive.FormatVersionProperty]) is not { } version || version != WebsiteExportArchive.FormatVersion)
        {
            problem = $"Its {WebsiteExportArchive.ManifestFileName} is from a version of Download everything this website doesn't read.";
            return false;
        }

        if (StringOf(file[WebsiteExportArchive.ListSeparatorProperty]) is not { Length: 1 } separator
            || file[WebsiteExportArchive.ArtworkFilesProperty] is not JsonArray artworkFiles)
        {
            return false;
        }

        var files = new List<(string, string)>();

        foreach (var entry in artworkFiles)
        {
            if (
                entry is not JsonObject artworkFile
                || StringOf(artworkFile[WebsiteExportArchive.FileProperty]) is not { } fileName
                || StringOf(artworkFile[WebsiteExportArchive.ArtworkTypeProperty]) is not { } artworkType
            )
            {
                return false;
            }

            files.Add((fileName, artworkType));
        }

        manifest = new WebsiteImportManifest(separator[0], files);
        return true;
    }

    private static string? StringOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() is JsonValueKind.String ? value.GetValue<string>() : null;

    private static int? IntOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() is JsonValueKind.Number && value.TryGetValue<int>(out var number) ? number : null;
}
