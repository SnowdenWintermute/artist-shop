namespace ArtistShop.Web.Components.Pages.Admin.Catalog.WorkPicker;

public enum WorkPickerMode
{
    // a new embed: the last step asks for its size and layout too
    Add,
    // only an image, asking nothing more: an existing embed's, which keeps its size and layout, or an
    // image component's
    ImageOnly,
    // the work itself, for a component's link to its page. The last step shows its main image
    WorkOnly,
}

// What the picker's steps carry in their addresses: why it was opened, and the list's own query
// string, without its "?", so a Back link returns to the list as it was
public record WorkPickerTrail(WorkPickerMode Mode, string? ListQuery)
{
    public const string ModeKey = "mode";
    public const string ListKey = "list";

    private const string ImageOnlyValue = "image-only";
    private const string WorkOnlyValue = "work-only";

    public static WorkPickerTrail Read(string? mode, string? listQuery) =>
        new(
            mode switch
            {
                ImageOnlyValue => WorkPickerMode.ImageOnly,
                WorkOnlyValue => WorkPickerMode.WorkOnly,
                _ => WorkPickerMode.Add,
            },
            listQuery
        );

    // the mode as it's written in an address, or null for adding, which is the default
    public string? ModeValue =>
        Mode switch
        {
            WorkPickerMode.ImageOnly => ImageOnlyValue,
            WorkPickerMode.WorkOnly => WorkOnlyValue,
            _ => null,
        };
}
