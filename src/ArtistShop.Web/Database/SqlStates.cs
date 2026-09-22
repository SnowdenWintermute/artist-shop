namespace ArtistShop.Web.Database;

// Every error code our functions RAISE, in one place: a code means one thing, whichever function
// raises it, and the repositories catch it by name instead of keeping their own copy. A code is
// Postgres's five-character SQLSTATE. Its first two characters are the class: the SQL standard
// leaves classes starting I to Z to implementations, Postgres takes only P0 and XX of those, and SH
// is ours. A code freed by a merge is reused, lowest first:
// nothing outside this repository has ever seen one, so its old meaning can't come back. Once this
// ships, a freed code stays retired, because a page loaded before a deploy could still be waiting
// on a reply that uses it.
public static class SqlStates
{
    // CheckArtworkChoicesAreCurrent, RenameVocabularyTerm
    public const string VocabularyTermNoLongerExists = "SH001";

    // UpdateVocabulary
    public const string VocabularyNoLongerExists = "SH002";

    // UpdateArtwork
    public const string ArtworkNoLongerExists = "SH003";

    // RenameSeries, CheckArtworkChoicesAreCurrent
    public const string SeriesNoLongerExists = "SH004";

    // ReorderSeriesArtworks
    public const string ArtworksChangedSincePageLoad = "SH005";

    // SetSeriesCover
    public const string ArtworkNoLongerInSeries = "SH006";

    // SetSeriesCover
    public const string ArtworkHasNoImage = "SH007";

    // ReorderSeries
    public const string SeriesChangedSincePageLoad = "SH008";

    // SH009 free: it was a second code for SeriesNoLongerExists

    // CheckArtworkChoicesAreCurrent, UpdateArtworkType, GetArtworkNameMatches,
    // AttachPrimaryImageToImagelessArtworkByName
    public const string ArtworkTypeNoLongerExists = "SH010";

    // CheckArtworkChoicesAreCurrent
    public const string ArtworkFieldSwitchedOff = "SH011";

    // AddArtwork
    public const string ProductTypeNoLongerExists = "SH012";

    // SH013 free: it was a second code for ArtworkTypeNoLongerExists

    // DeleteArtworkType
    public const string ArtworkTypeInUse = "SH014";

    // UpdatePost
    public const string PostNoLongerExists = "SH015";
}
