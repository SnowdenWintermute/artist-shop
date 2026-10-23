namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Domain.Website;

// which side of its slot a heading's text or a button sits on, as Tailwind classes spelled out so Tailwind finds them
public static class ComponentAlignmentClasses
{
    // for text, as a heading's
    public static string Text(ComponentAlignment alignment) =>
        alignment switch
        {
            ComponentAlignment.Left => "text-left",
            ComponentAlignment.Center => "text-center",
            ComponentAlignment.Right => "text-right",
            _ => throw new InvalidOperationException($"{alignment} has no class."),
        };

    // for a flex row's child, as a button's
    public static string Flex(ComponentAlignment alignment) =>
        alignment switch
        {
            ComponentAlignment.Left => "justify-start",
            ComponentAlignment.Center => "justify-center",
            ComponentAlignment.Right => "justify-end",
            _ => throw new InvalidOperationException($"{alignment} has no class."),
        };
}
