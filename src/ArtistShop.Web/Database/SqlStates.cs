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
    // CheckWorkChoicesAreCurrent, RenameVocabularyTerm
    public const string VocabularyTermNoLongerExists = "SH001";

    // UpdateVocabulary
    public const string VocabularyNoLongerExists = "SH002";

    // UpdateWork
    public const string WorkNoLongerExists = "SH003";

    // RenameCollection, CheckWorkChoicesAreCurrent, AddWorksToCollection
    public const string CollectionNoLongerExists = "SH004";

    // ReorderCollectionWorks
    public const string WorksChangedSincePageLoad = "SH005";

    // SetCollectionCover
    public const string WorkNoLongerInCollection = "SH006";

    // SetCollectionCover
    public const string WorkHasNoImage = "SH007";

    // ReorderCollections
    public const string CollectionChangedSincePageLoad = "SH008";

    // SH009 free: it was a second code for CollectionNoLongerExists

    // CheckWorkChoicesAreCurrent, UpdateWorkType, GetWorkNameMatches,
    // AttachPrimaryImageToImagelessWorkByName
    public const string WorkTypeNoLongerExists = "SH010";

    // CheckWorkChoicesAreCurrent
    public const string WorkFieldSwitchedOff = "SH011";

    // AddWork
    public const string ProductTypeNoLongerExists = "SH012";

    // SH013 free: it was a second code for WorkTypeNoLongerExists

    // DeleteWorkType
    public const string WorkTypeInUse = "SH014";

    // UpdatePost
    public const string PostNoLongerExists = "SH015";

    // AddSiteWithSignUpCode, in the platform database
    public const string SignUpCodeNotUsable = "SH016";

    // ReorderExampleSites, in the platform database
    public const string ExampleSitesChangedSincePageLoad = "SH017";

    // CheckWorkChoicesAreCurrent
    public const string VocabularyBecameMutuallyExclusive = "SH018";

    // UpdateTheme, UseTheme
    public const string ThemeNoLongerExists = "SH019";
}
