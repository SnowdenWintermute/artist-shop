namespace ArtistShop.Web.Domain.Website;

// What the website calls collections, works and a work's description, as the artist chose on the Wording page
public record SiteWording(NounChoice CollectionChoice, NounChoice WorkChoice, LabelChoice DescriptionChoice)
{
    public const string DefaultCollectionSingular = "Collection";
    public const string DefaultCollectionPlural = "Collections";
    public const string DefaultWorkSingular = "Work";
    public const string DefaultWorkPlural = "Works";
    public const string DefaultDescriptionLabel = "Artist statement";

    public static readonly SiteWording Default = new(NounChoice.Default, NounChoice.Default, LabelChoice.Default);

    public DisplayNoun Collection => DisplayNoun.From(CollectionChoice, DefaultCollectionSingular, DefaultCollectionPlural);
    public DisplayNoun Work => DisplayNoun.From(WorkChoice, DefaultWorkSingular, DefaultWorkPlural);
    public DisplayLabel Description => DisplayLabel.From(DescriptionChoice, DefaultDescriptionLabel);
}
