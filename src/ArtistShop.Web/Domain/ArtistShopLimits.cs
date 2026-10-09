namespace ArtistShop.Web.Domain;

// updates to these must be mirrored in sql
public static class ArtistShopLimits
{
    public const int WorkImageFileNameMaximumLength = 260;
    public const int WorkNameMaximumLength = 200;
    public const int SlugMaximumLength = 200;
    public const int BaseSlugMaximumLength = SlugMaximumLength - 10;

    public const string MinimumPrice = "0";
    public const string MaximumPrice = "99999999.99";

    public const int MinimumStock = 0;

    public const string MinimumDimensionCm = "0.01";
    public const string MaximumDimensionCm = "9999.9999";

    // the size columns are numeric(8, 4), which round anything finer
    public const int DimensionDecimalPlaces = 4;

    public const int VocabularyNameMaximumLength = 100;
    public const int VocabularyTermNameMaximumLength = 100;

    public const int WorkTypeNameMaximumLength = 50;

    public const int CollectionNameMaximumLength = 256;

    // themes.name
    public const int ThemeNameMaximumLength = 100;

    // wording's word columns
    public const int WordingWordMaximumLength = 40;

    public const int WorkListPageSize = 25;

    public const int BlogPageSize = 10;

    public const int PostTitleMaximumLength = 200;

    // sign_up_codes.note
    public const int SignUpCodeNoteMaximumLength = 200;

    // site_invites.email: the longest address email allows
    public const int EmailMaximumLength = 254;

    // site_member_input_values.value
    public const int RememberedInputValueMaximumLength = 100;
}
