namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages.Workspace;

using ArtistShop.Web.Domain.Website;

// What the outline draws of a row: its split, its slots and their components' names. Small and plain, unlike a
// PageRow, whose components carry their settings, so persisted component state can carry it across from prerendering
public record RowSketch(PageRowId Id, RowWidths Widths, List<SlotSketch> Slots)
{
    public static RowSketch Of(PageRow row) =>
        new(
            row.Id,
            row.Widths,
            [.. row.Slots.Select(slot => new SlotSketch(slot.Id, [.. slot.Stack.Select(component => new ComponentSketch(component.Id, component.Name))]))]
        );
}

public record SlotSketch(RowSlotId Id, List<ComponentSketch> Components);

public record ComponentSketch(CustomComponentId Id, string Name);
