namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages.Workspace;

// Written out whole for Tailwind
public static class OutlineClasses
{
    // a row's or component's border, drawn inward when it's the part being edited, so it keeps its size
    public static string BorderOf(bool isEdited) => isEdited ? "border-accent outline-2 -outline-offset-2 outline-accent" : "border-rule";

    // The places a dragged part can land (PageOutline.razor.js): the gaps before each part, there all along so
    // nothing changes size, and the Add links at each list's end. They show only while a part that can land there is
    // dragged (data-drop-ok), and fill under the pointer (data-drop-active), stacked on data-drop-ok so it outranks it
    private const string Lit = "rounded-sm data-drop-ok:bg-accent/25 data-drop-active:data-drop-ok:bg-accent";

    public const string RowGap = "h-3 " + Lit;

    public const string ComponentGap = "h-2 " + Lit;

    public const string SlotGap = "w-2 shrink-0 self-stretch " + Lit;

    // an Add link, lit the same as a gap; the padding gives the colour room and the margin takes it back, so the link
    // keeps its place
    public const string End = "-mx-1 px-1 data-drop-active:data-drop-ok:text-on-accent " + Lit;
}
