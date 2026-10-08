namespace ArtistShop.Web.Domain.Catalog;

public record WorkTypeId(int Value);

public record WorkTypeName(string Value);

public record WorkType(WorkTypeId Id, WorkTypeName Name);

// a list, not a set: passed to interactive islands as JSON
public record WorkTypeWithFields(
    WorkTypeId Id,
    WorkTypeName Name,
    IReadOnlyList<WorkField> Fields
);

public record WorkTypeWorkCounts(
    int Total,
    int WithDateCreated,
    int WithHeightAndWidth,
    int WithDepth,
    int WithDuration
)
{
    public int WithValueIn(WorkField field) =>
        field switch
        {
            WorkField.DateCreated => WithDateCreated,
            WorkField.HeightAndWidth => WithHeightAndWidth,
            WorkField.Depth => WithDepth,
            WorkField.Duration => WithDuration,
        };
}
