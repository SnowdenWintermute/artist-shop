namespace ArtistShop.Web.Database;

// Every number our procedures THROW, in one place: a number means one thing, whichever procedure
// raises it, and the repositories catch it by name instead of keeping their own copy. 50000 and
// above are free for our own errors. A retired number is never given a new meaning, because an
// older page or client could still be waiting on a reply that uses the old one.
public static class SqlErrorNumbers
{
    // AddArtwork, RenameVocabularyTerm
    public const int VocabularyTermNoLongerExists = 50001;

    // UpdateVocabulary
    public const int VocabularyNoLongerExists = 50002;

    // 50003 retired: it was a second number for VocabularyTermNoLongerExists

    // RenameSeries, AddArtwork
    public const int SeriesNoLongerExists = 50004;

    // ReorderSeriesArtworks
    public const int ArtworksChangedSincePageLoad = 50005;

    // SetSeriesCover
    public const int ArtworkNoLongerInSeries = 50006;

    // SetSeriesCover
    public const int ArtworkHasNoImage = 50007;

    // ReorderSeries
    public const int SeriesChangedSincePageLoad = 50008;

    // 50009 retired: it was a second number for SeriesNoLongerExists

    // AddArtwork, UpdateArtworkType, GetArtworkNameMatches,
    // AttachPrimaryImageToImagelessArtworkByName
    public const int ArtworkTypeNoLongerExists = 50010;

    // AddArtwork
    public const int ArtworkFieldSwitchedOff = 50011;

    // AddArtwork
    public const int ProductTypeNoLongerExists = 50012;

    // 50013 retired: it was a second number for ArtworkTypeNoLongerExists

    // DeleteArtworkType
    public const int ArtworkTypeInUse = 50014;
}
