namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Works;

using ArtistShop.Web.Domain.Catalog;

// The works checked in a WorkListBrowser, kept in the address as one FieldName parameter per
// work, so they stay checked while the filters and the page change
public record WorkListSelection(string FieldName, IReadOnlyList<WorkId> Ids)
{
    public bool Contains(WorkId id) => Ids.Contains(id);
}
