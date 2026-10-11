using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArtistShop.Web.Exports;
using static ArtistShop.Web.Imports.ImportJson;

namespace ArtistShop.Web.Imports;

// website.json from Download everything: the list separator and each work file's type
public record WebsiteImportManifest(char ListSeparator, IReadOnlyList<(string FileName, string WorkType)> WorkFiles)
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
            || file[WebsiteExportArchive.WorkFilesProperty] is not JsonArray workFiles)
        {
            return false;
        }

        var files = new List<(string, string)>();

        foreach (var entry in workFiles)
        {
            if (
                entry is not JsonObject workFile
                || StringOf(workFile[WebsiteExportArchive.FileProperty]) is not { } fileName
                || StringOf(workFile[WebsiteExportArchive.WorkTypeProperty]) is not { } workType
            )
            {
                return false;
            }

            files.Add((fileName, workType));
        }

        manifest = new WebsiteImportManifest(separator[0], files);
        return true;
    }
}
