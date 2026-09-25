namespace ArtistShop.Web.Domain;

// updates to these must be mirrored in sql
public static class ArtistShopLimits
{
    public const int ArtworkImageFileNameMaximumLength = 260;
    public const int ArtworkNameMaximumLength = 200;
    public const int SlugMaximumLength = 200;
    public const int BaseSlugMaximumLength = SlugMaximumLength - 10;

    public const string MinimumPrice = "0";
    public const string MaximumPrice = "99999999.99";

    public const int MinimumStock = 0;

    public const string MinimumDimensionCm = "0.01";
    public const string MaximumDimensionCm = "9999.9999";

    public const int VocabularyNameMaximumLength = 100;
    public const int VocabularyTermNameMaximumLength = 100;

    public const int ArtworkTypeNameMaximumLength = 50;

    public const int SeriesNameMaximumLength = 256;

    public const int ArtworkListPageSize = 25;

    public const int BlogPageSize = 10;

    public const int PostTitleMaximumLength = 200;

    // sign_up_codes.note
    public const int SignUpCodeNoteMaximumLength = 200;

    // site_invites.email: the longest address email allows
    public const int EmailMaximumLength = 254;
}
