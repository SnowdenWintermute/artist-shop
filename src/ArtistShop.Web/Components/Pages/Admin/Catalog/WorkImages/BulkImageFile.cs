using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.WorkImages;

// One file as the page shows it: the plan's outcome at first, then the server's answer once it's
// uploaded
public class BulkImageFile(BulkImagePlanItem planned)
{
    public string Id { get; } = planned.Id;
    public string Path { get; } = planned.Path;
    public BulkImageOutcome Outcome { get; set; } = planned.Outcome;
    public string? Error { get; set; }

    // the works the name matched, so the report can link to them
    public IReadOnlyList<int> WorkIds { get; set; } = planned.WorkIds;

    // for another image of a work, like "Dawn (2)", its number, which orders it after the others
    public int? ExtraNumber { get; } = planned.ExtraNumber;
}
