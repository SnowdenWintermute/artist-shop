namespace ArtistShop.Web.Domain.Catalog;

// the values must match the ids seeded into dbo.ArtworkFields
public enum ArtworkField
{
    DateCreated = 1,
    HeightAndWidth = 2,
    Depth = 3,
    Duration = 4,
}

// RequiredField is the field itself when it needs no other
public record ArtworkFieldDefinition(ArtworkField Field, string Name, ArtworkField RequiredField)
{
    public bool HasRequirement => RequiredField != Field;
}
