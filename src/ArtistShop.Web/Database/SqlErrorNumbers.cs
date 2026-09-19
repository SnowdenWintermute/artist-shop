namespace ArtistShop.Web.Database;

// Every number our procedures THROW, in one place: a number means one thing, whichever procedure
// raises it, and the repositories catch it by name instead of keeping their own copy. 50000 and
// above are free for our own errors. A number freed by a merge is reused, lowest first: nothing
// outside this repository has ever seen one, so its old meaning can't come back. Once this ships,
// a freed number stays retired, because a page loaded before a deploy could still be waiting on a
// reply that uses it.
public static class SqlErrorNumbers
{
    // CheckArtworkChoicesAreCurrent, RenameVocabularyTerm
    public const int VocabularyTermNoLongerExists = 50001;

    // UpdateVocabulary
    public const int VocabularyNoLongerExists = 50002;

    // UpdateArtwork
    public const int ArtworkNoLongerExists = 50003;

    // RenameSeries, CheckArtworkChoicesAreCurrent
    public const int SeriesNoLongerExists = 50004;

    // ReorderSeriesArtworks
    public const int ArtworksChangedSincePageLoad = 50005;

    // SetSeriesCover
    public const int ArtworkNoLongerInSeries = 50006;

    // SetSeriesCover
    public const int ArtworkHasNoImage = 50007;

    // ReorderSeries
    public const int SeriesChangedSincePageLoad = 50008;

    // 50009 free: it was a second number for SeriesNoLongerExists

    // CheckArtworkChoicesAreCurrent, UpdateArtworkType, GetArtworkNameMatches,
    // AttachPrimaryImageToImagelessArtworkByName
    public const int ArtworkTypeNoLongerExists = 50010;

    // CheckArtworkChoicesAreCurrent
    public const int ArtworkFieldSwitchedOff = 50011;

    // AddArtwork
    public const int ProductTypeNoLongerExists = 50012;

    // 50013 free: it was a second number for ArtworkTypeNoLongerExists

    // DeleteArtworkType
    public const int ArtworkTypeInUse = 50014;
}
