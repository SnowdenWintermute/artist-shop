namespace ArtistShop.Web.Components.Hints;

// what the Hints page says about each hint: the switches have no default, so a new HintType fails
// the build until it's given both
public static class HintListing
{
    public static string Title(HintType type) =>
        type switch
        {
            HintType.AdminDashboard => "Where to start",
            HintType.AddArtworksFromImages => "Adding artworks from images",
            HintType.SeriesFromFolders => "Series from folder names",
            HintType.UploadArtworkImages => "Uploading images for artworks already added",
            HintType.Series => "What series are",
            HintType.Vocabularies => "What vocabularies are",
            HintType.StepByStepImport => "Importing step by step",
            HintType.ArtworkTypeImport => "Importing artwork types",
            HintType.VocabularyImport => "Importing vocabularies",
            HintType.ArtworkImport => "Importing artworks",
            HintType.PostImport => "Importing posts",
            HintType.WebsiteImport => "Moving a whole website here",
        };

    // the page it shows on
    public static string PageUrl(HintType type) =>
        type switch
        {
            HintType.AdminDashboard => PageUrls.AdminDashboard,
            HintType.AddArtworksFromImages => PageUrls.AddArtworksFromImages,
            // in the dialog that opens after dropping folders there
            HintType.SeriesFromFolders => PageUrls.AddArtworksFromImages,
            HintType.UploadArtworkImages => PageUrls.ArtworkBulkImageUpload(null),
            HintType.Series => PageUrls.SeriesList,
            HintType.Vocabularies => PageUrls.NewVocabulary,
            HintType.StepByStepImport => PageUrls.Import,
            HintType.ArtworkTypeImport => PageUrls.ArtworkTypeImport,
            HintType.VocabularyImport => PageUrls.VocabularyImport,
            HintType.ArtworkImport => PageUrls.ArtworkImport,
            HintType.PostImport => PageUrls.PostImport,
            HintType.WebsiteImport => PageUrls.WebsiteImport,
        };
}
