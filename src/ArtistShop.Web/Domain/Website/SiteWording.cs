namespace ArtistShop.Web.Domain.Website;

// What the website calls collections and works, as the artist chose on the Wording page
public record SiteWording(NounChoice CollectionChoice, NounChoice WorkChoice)
{
    public const string DefaultCollectionSingular = "Collection";
    public const string DefaultCollectionPlural = "Collections";
    public const string DefaultWorkSingular = "Work";
    public const string DefaultWorkPlural = "Works";

    public static readonly SiteWording Default = new(NounChoice.Default, NounChoice.Default);

    public DisplayNoun Collection => DisplayNoun.From(CollectionChoice, DefaultCollectionSingular, DefaultCollectionPlural);
    public DisplayNoun Work => DisplayNoun.From(WorkChoice, DefaultWorkSingular, DefaultWorkPlural);
}
