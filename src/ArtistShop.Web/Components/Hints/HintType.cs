namespace ArtistShop.Web.Components.Hints;

// Each Hint on the admin pages, in the order the Hints page lists them. The names are stored for
// the hints an account dismissed, so renaming one shows it again to everyone who dismissed it
public enum HintType
{
    AdminDashboard,
    AddArtworksFromImages,
    SeriesFromFolders,
    UploadArtworkImages,
    Series,
    Vocabularies,
    StepByStepImport,
    ArtworkTypeImport,
    VocabularyImport,
    ArtworkImport,
    PostImport,
    WebsiteImport,
}
