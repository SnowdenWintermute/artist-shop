namespace ArtistShop.Web.Domain.Catalog;

// the values must match the match types in get_work_name_match_types and
// attach_primary_image_to_imageless_work_by_name
public enum WorkNameMatchType : byte
{
    OneImagelessWork = 1,
    NoWork = 2,
    SeveralWorks = 3,
    WorkWithImages = 4,
}

public record WorkNameMatch(WorkNameMatchType Type, IReadOnlyList<WorkId> WorkIds);

// what the attach procedure did with one file. It classifies the name the same way the pre-check
// does, and the one-imageless-work case is the one where the image was attached
public record ImageAttachResult(WorkNameMatchType MatchType, IReadOnlyList<WorkId> WorkIds)
{
    public bool Attached => MatchType is WorkNameMatchType.OneImagelessWork;
}
