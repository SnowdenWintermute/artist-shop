namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

using ArtistShop.Web.Domain.Website;

// What the page editor's row list draws of a row: its split and its components' names. Small and plain, unlike a
// PageRow, whose components carry their settings, so persisted component state can carry it across from prerendering
public record RowSketch(PageRowId Id, RowWidths Widths, List<SlotSketch> Slots)
{
    public static RowSketch Of(PageRow row) =>
        new(row.Id, row.Widths, [.. row.Slots.Select(slot => new SlotSketch(slot.Id, [.. slot.Stack.Select(component => component.Name)]))]);
}

public record SlotSketch(RowSlotId Id, List<string> ComponentNames);
