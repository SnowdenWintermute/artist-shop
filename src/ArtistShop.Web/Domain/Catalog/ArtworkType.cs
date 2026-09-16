namespace ArtistShop.Web.Domain.Catalog;

public record ArtworkTypeId(int Value);

public record ArtworkTypeName(string Value);

public record ArtworkType(ArtworkTypeId Id, ArtworkTypeName Name);

// a list, not a set: passed to interactive islands as JSON
public record ArtworkTypeWithFields(
    ArtworkTypeId Id,
    ArtworkTypeName Name,
    IReadOnlyList<ArtworkField> Fields
);

public record ArtworkTypeArtworkCounts(
    int Total,
    int WithDateCreated,
    int WithHeightAndWidth,
    int WithDepth,
    int WithDuration
)
{
    public int WithValueIn(ArtworkField field) =>
        field switch
        {
            ArtworkField.DateCreated => WithDateCreated,
            ArtworkField.HeightAndWidth => WithHeightAndWidth,
            ArtworkField.Depth => WithDepth,
            ArtworkField.Duration => WithDuration,
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
        };
}
