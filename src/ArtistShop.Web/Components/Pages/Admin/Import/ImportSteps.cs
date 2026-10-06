namespace ArtistShop.Web.Components.Pages.Admin.Import;

// Title is the step's tab, like "1. Artwork types". Action reads "Import vocabularies" for the buttons
// "Import vocabularies first" and "Import vocabularies next"
public record ImportStepPage(string Title, string Href, string Action);

// the step-by-step imports, in the order they must run
public static class ImportSteps
{
    public static readonly ImportStepPage ArtworkTypes = new(
        "1. Artwork types",
        PageUrls.ArtworkTypeImport,
        "Import artwork types"
    );

    public static readonly ImportStepPage Vocabularies = new(
        "2. Vocabularies",
        PageUrls.VocabularyImport,
        "Import vocabularies"
    );

    public static readonly ImportStepPage Artworks = new("3. Artworks", PageUrls.ArtworkImport, "Import artworks");

    public static readonly ImportStepPage Images = new(
        "4. Images",
        PageUrls.ArtworkBulkImageUpload(typeId: null),
        "Upload images"
    );

    public static readonly ImportStepPage Posts = new("5. Posts", PageUrls.PostImport, "Import posts");

    public static readonly ImportStepPage[] All = [ArtworkTypes, Vocabularies, Artworks, Images, Posts];

    // null for the first step
    public static ImportStepPage? Previous(ImportStepPage step) => All.ElementAtOrDefault(Array.IndexOf(All, step) - 1);

    // null for the last step
    public static ImportStepPage? Next(ImportStepPage step) => All.ElementAtOrDefault(Array.IndexOf(All, step) + 1);
}
