namespace ArtistShop.Web.Components.Pages.Admin.Import;

using ArtistShop.Web.Domain.Website;

// Title is the step's tab, like "2. Work types". Action reads "Import vocabularies" for the buttons
// "Import vocabularies first" and "Import vocabularies next". Both in the website's own words
public record ImportStepPage(Func<SiteWording, string> Title, string Href, Func<SiteWording, string> Action);

// the step-by-step imports, in the order they must run
public static class ImportSteps
{
    public static readonly ImportStepPage Look = new(
        _ => "1. Look and wording",
        PageUrls.LookImport,
        _ => "Import the look and wording"
    );

    public static readonly ImportStepPage WorkTypes = new(
        wording => $"2. {wording.Work.SingularHeading} types",
        PageUrls.WorkTypeImport,
        wording => $"Import {wording.Work.SingularInSentence} types"
    );

    public static readonly ImportStepPage Vocabularies = new(
        _ => "3. Vocabularies",
        PageUrls.VocabularyImport,
        _ => "Import vocabularies"
    );

    public static readonly ImportStepPage Works = new(
        wording => $"4. {wording.Work.PluralHeading}",
        PageUrls.WorkImport,
        wording => $"Import {wording.Work.PluralInSentence}"
    );

    public static readonly ImportStepPage Images = new(
        _ => "5. Images",
        PageUrls.WorkBulkImageUpload(typeId: null),
        _ => "Upload images"
    );

    public static readonly ImportStepPage Posts = new(_ => "6. Posts", PageUrls.PostImport, _ => "Import posts");

    public static readonly ImportStepPage[] All = [Look, WorkTypes, Vocabularies, Works, Images, Posts];

    // null for the first step
    public static ImportStepPage? Previous(ImportStepPage step) => All.ElementAtOrDefault(Array.IndexOf(All, step) - 1);

    // null for the last step
    public static ImportStepPage? Next(ImportStepPage step) => All.ElementAtOrDefault(Array.IndexOf(All, step) + 1);
}
