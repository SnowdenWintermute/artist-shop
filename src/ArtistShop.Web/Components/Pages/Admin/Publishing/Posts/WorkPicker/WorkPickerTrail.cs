namespace ArtistShop.Web.Components.Pages.Admin.Publishing.Posts.WorkPicker;

public enum WorkPickerMode
{
    // a new embed: the last step asks for its size and layout too
    Add,
    // an existing embed's image: it keeps its size and layout
    ChangeImage,
}

// What the picker's steps carry in their addresses: why it was opened, and the list's own query
// string, without its "?", so a Back link returns to the list as it was
public record WorkPickerTrail(WorkPickerMode Mode, string? ListQuery)
{
    public const string ModeKey = "mode";
    public const string ListKey = "list";

    private const string ChangeImageValue = "change-image";

    public static WorkPickerTrail Read(string? mode, string? listQuery) =>
        new(mode == ChangeImageValue ? WorkPickerMode.ChangeImage : WorkPickerMode.Add, listQuery);

    // the mode as it's written in an address, or null for adding, which is the default
    public string? ModeValue => Mode is WorkPickerMode.ChangeImage ? ChangeImageValue : null;
}
