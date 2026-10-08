using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Exports;

namespace ArtistShop.Web.Imports;

// website.json from Download everything: the list separator, each work file's type, and the wording
public record WebsiteImportManifest(
    char ListSeparator,
    IReadOnlyList<(string FileName, string WorkType)> WorkFiles,
    SiteWording Wording
)
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
            || file[WebsiteExportArchive.WorkFilesProperty] is not JsonArray workFiles
            || file[WebsiteExportArchive.WordingProperty] is not JsonObject wording
            || NounOf(wording[WebsiteExportArchive.CollectionWordingProperty]) is not { } collectionChoice
            || NounOf(wording[WebsiteExportArchive.WorkWordingProperty]) is not { } workChoice)
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

        manifest = new WebsiteImportManifest(separator[0], files, new SiteWording(collectionChoice, workChoice));
        return true;
    }

    // null for anything the Wording page couldn't have saved
    private static NounChoice? NounOf(JsonNode? node)
    {
        if (
            node is not JsonObject noun
            || !TryWordOf(noun[WebsiteExportArchive.SingularProperty], out var singular)
            || !TryWordOf(noun[WebsiteExportArchive.PluralProperty], out var plural)
            || (singular is null) != (plural is null)
            || BoolOf(noun[WebsiteExportArchive.KeepsCaseProperty]) is not { } keepsCase
        )
        {
            return null;
        }

        return new NounChoice(singular, plural, keepsCase);
    }

    // a missing word is null, the default; one that's there has to be a word the database takes
    private static bool TryWordOf(JsonNode? node, out string? word)
    {
        word = StringOf(node);
        return node is null
            || word is { Length: <= ArtistShopLimits.WordingWordMaximumLength } && !string.IsNullOrWhiteSpace(word);
    }

    private static bool? BoolOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False ? value.GetValue<bool>() : null;

    private static string? StringOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() is JsonValueKind.String ? value.GetValue<string>() : null;

    private static int? IntOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() is JsonValueKind.Number && value.TryGetValue<int>(out var number) ? number : null;
}
