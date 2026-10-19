namespace ArtistShop.Web.Components.Hints;

using ArtistShop.Web.Domain.Platform;
using ArtistShop.Web.Domain.Website;

// what the Hints page says about each hint: the switches have no default, so a new HintType fails
// the build until it's given both. Titles are in the website's words
public static class HintListing
{
    public static string Title(HintType type, SiteWording wording) =>
        type switch
        {
            HintType.AdminDashboard => "Where to start",
            HintType.AddWorksFromImages => $"Adding {wording.Work.PluralInSentence} from images",
            HintType.CollectionsFromFolders =>
                $"{wording.Collection.PluralHeading} from folder names",
            HintType.UploadWorkImages =>
                $"Uploading images for {wording.Work.PluralInSentence} already added",
            HintType.Collections => $"What {wording.Collection.PluralInSentence} are",
            HintType.Vocabularies => "What vocabularies are",
            HintType.VocabularyTerms => "Adding terms to a vocabulary",
            HintType.StepByStepImport => "Importing step by step",
            HintType.WorkTypeImport => $"Importing {wording.Work.SingularInSentence} types",
            HintType.VocabularyImport => "Importing vocabularies",
            HintType.WorkImport => $"Importing {wording.Work.PluralInSentence}",
            HintType.PostImport => "Importing posts",
            HintType.WebsiteImport => "Moving a whole website here",
            HintType.WordingCollections => $"Wording for {wording.Collection.PluralInSentence}",
            HintType.WordingWorks => $"Wording for {wording.Work.PluralInSentence}",
            HintType.WordingDescription => $"Wording for the {wording.Description.InSentence}",
            HintType.LookAndWordingImport => "Importing look and wording",
            HintType.CustomPages => "How pages are built",
            HintType.Images => "What the Images page is for",
            HintType.FullBleedWorkImages => $"{wording.Work.SingularHeading} images shown wider on pages",
        };

    // the page it shows on
    public static string PageUrl(HintType type) =>
        type switch
        {
            HintType.AdminDashboard => PageUrls.AdminDashboard,
            HintType.AddWorksFromImages => PageUrls.AddWorksFromImages,
            // in the dialog that opens after dropping folders there
            HintType.CollectionsFromFolders => PageUrls.AddWorksFromImages,
            HintType.UploadWorkImages => PageUrls.WorkBulkImageUpload(null),
            HintType.Collections => PageUrls.CollectionList,
            HintType.Vocabularies => PageUrls.NewVocabulary,
            // in the vocabulary dialog's Add term step, after saving a new vocabulary there
            HintType.VocabularyTerms => PageUrls.AddWorksFromImages,
            HintType.StepByStepImport => PageUrls.Import,
            HintType.WorkTypeImport => PageUrls.WorkTypeImport,
            HintType.VocabularyImport => PageUrls.VocabularyImport,
            HintType.WorkImport => PageUrls.WorkImport,
            HintType.PostImport => PageUrls.PostImport,
            HintType.WebsiteImport => PageUrls.WebsiteImport,
            HintType.WordingCollections => PageUrls.Wording,
            HintType.WordingWorks => PageUrls.Wording,
            HintType.WordingDescription => PageUrls.Wording,
            HintType.LookAndWordingImport => PageUrls.LookImport,
            HintType.CustomPages => PageUrls.CustomPageList,
            HintType.Images => PageUrls.Images,
            // on a row's editor or an image component's settings, reached from there
            HintType.FullBleedWorkImages => PageUrls.CustomPageList,
        };
}
