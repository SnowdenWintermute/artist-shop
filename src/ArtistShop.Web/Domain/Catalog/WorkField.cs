namespace ArtistShop.Web.Domain.Catalog;

// the values must match the ids seeded into work_fields
public enum WorkField
{
    DateCreated = 1,
    HeightAndWidth = 2,
    Depth = 3,
    Duration = 4,
}

// RequiredField is the field itself when it needs no other
public record WorkFieldDefinition(WorkField Field, string Name, WorkField RequiredField)
{
    public bool HasRequirement => RequiredField != Field;
}
