namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

// The editors' pressed buttons light blue, as a chosen font is, else light grey. Written out whole for Tailwind
public static class ChoiceClasses
{
    // on a button with aria-pressed
    public const string Pressable = "bg-placeholder aria-pressed:bg-selected";

    // on a part of a "group" button with aria-pressed
    public const string InPressable = "bg-placeholder group-aria-pressed:bg-selected";
}
